using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Notificaciones;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Excepcion;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace PER.Comandos.LineaComandos.Cola.Procesadores
{
    public class ProcesadorColaComandos
    {
        private static readonly TimeSpan EsperaReintentoCarga = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IColaComandosMemoria _colaComandosMemoria;
        private readonly IPublicadorNotificacionEjecucionComandos _publicadorNotificaciones;
        private readonly ILogger<ProcesadorColaComandos> _logger;
        private readonly int _maxParalelismo;
        private readonly TimeSpan _umbralComandoLargaDuracion;
        private readonly int _maxTareasLargaDuracion;
        private readonly ConcurrentDictionary<Task, byte> _tareasEnEjecucion;
        private readonly ConcurrentDictionary<Task, long> _tareasLargaDuracion;

        public ProcesadorColaComandos(
            IServiceScopeFactory serviceScopeFactory,
            IColaComandosMemoria colaComandosMemoria,
            IPublicadorNotificacionEjecucionComandos publicadorNotificaciones,
            int maxParalelismo,
            TimeSpan umbralComandoLargaDuracion,
            int maxTareasLargaDuracion,
            ILogger<ProcesadorColaComandos> logger)
        {
            if (maxParalelismo <= 0)
                throw new ArgumentException("El máximo paralelismo debe ser mayor a cero", nameof(maxParalelismo));

            if (umbralComandoLargaDuracion <= TimeSpan.Zero)
                throw new ArgumentException("El umbral de larga duración debe ser mayor a cero", nameof(umbralComandoLargaDuracion));

            if (maxTareasLargaDuracion <= 0)
                throw new ArgumentException("El máximo de tareas de larga duración debe ser mayor a cero", nameof(maxTareasLargaDuracion));

            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _colaComandosMemoria = colaComandosMemoria ?? throw new ArgumentNullException(nameof(colaComandosMemoria));
            _publicadorNotificaciones = publicadorNotificaciones ?? throw new ArgumentNullException(nameof(publicadorNotificaciones));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxParalelismo = maxParalelismo;
            _umbralComandoLargaDuracion = umbralComandoLargaDuracion;
            _maxTareasLargaDuracion = maxTareasLargaDuracion;
            _tareasEnEjecucion = new ConcurrentDictionary<Task, byte>();
            _tareasLargaDuracion = new ConcurrentDictionary<Task, long>();
        }

        public async Task StartAsync(CancellationToken token)
        {
            _logger.LogInformation(
                "ProcesadorColaComandos iniciado con paralelismo máximo de {MaxParalelismo}, umbral de larga duración de {UmbralLargaDuracion} y máximo de {MaxTareasLargaDuracion} tareas largas",
                _maxParalelismo,
                _umbralComandoLargaDuracion,
                _maxTareasLargaDuracion);

            await CargarPendientesConReintentoAsync(token);

            using SemaphoreSlim semaforoParalelismo = new(_maxParalelismo, _maxParalelismo);
            using SemaphoreSlim semaforoLargaDuracion = new(_maxTareasLargaDuracion, _maxTareasLargaDuracion);

            try
            {
                await foreach (ComandoEnCola comando in _colaComandosMemoria.LeerAsync(token))
                {
                    LimpiarTareasCompletadas();

                    await semaforoParalelismo.WaitAsync(token);

                    Task tarea = ProcesarComandoConLiberacionAsync(
                        comando,
                        semaforoParalelismo,
                        semaforoLargaDuracion,
                        token);

                    _tareasEnEjecucion.TryAdd(tarea, 0);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.LogWarning("ProcesadorColaComandos cancelado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el consumo de comandos en memoria");
            }

            Task[] tareasPendientes = _tareasEnEjecucion.Keys
                .Concat(_tareasLargaDuracion.Keys)
                .Distinct()
                .ToArray();

            if (tareasPendientes.Length > 0)
            {
                _logger.LogInformation("Esperando que terminen {Count} tareas pendientes", tareasPendientes.Length);
                try
                {
                    await Task.WhenAll(tareasPendientes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Una o más tareas de comandos finalizaron con error no controlado");
                }
            }

            _logger.LogInformation("ProcesadorColaComandos finalizado");
        }

        private async Task CargarPendientesConReintentoAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _colaComandosMemoria.CargarPendientesDesdeBaseDatosAsync(token);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error cargando comandos pendientes desde base de datos. Se reintentará en {EsperaTotalSegundos} segundos.",
                        EsperaReintentoCarga.TotalSeconds);

                    await Task.Delay(EsperaReintentoCarga, token);
                }
            }
        }

        private async Task ProcesarComandoConLiberacionAsync(
            ComandoEnCola comando,
            SemaphoreSlim semaforoParalelismo,
            SemaphoreSlim semaforoLargaDuracion,
            CancellationToken token)
        {
            bool cupoParalelismoLiberado = false;
            bool cupoLargaDuracionAdquirido = false;

            Task tareaEjecucion = ProcesarComandoAsync(comando, token);

            using CancellationTokenSource cancelarEsperaUmbral = new();

            try
            {
                Task esperaUmbral = Task.Delay(_umbralComandoLargaDuracion, cancelarEsperaUmbral.Token);

                Task primeraTarea = await Task.WhenAny(tareaEjecucion, esperaUmbral);

                if (primeraTarea == tareaEjecucion || tareaEjecucion.IsCompleted)
                {
                    cancelarEsperaUmbral.Cancel();
                    await tareaEjecucion;
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    await tareaEjecucion;
                    return;
                }

                _logger.LogInformation(
                    "El comando `{ComandoId}` superó el umbral de larga duración {UmbralLargaDuracion} y espera un cupo en las tareas de largas duración.",
                    comando.Id,
                    _umbralComandoLargaDuracion);

                using CancellationTokenSource cancelarEsperaCupo = CancellationTokenSource.CreateLinkedTokenSource(token);
                Task esperaCupoLargaDuracion = semaforoLargaDuracion.WaitAsync(cancelarEsperaCupo.Token);
                Task siguienteTarea = await Task.WhenAny(tareaEjecucion, esperaCupoLargaDuracion);

                if (siguienteTarea == tareaEjecucion || tareaEjecucion.IsCompleted)
                {
                    cancelarEsperaCupo.Cancel();
                    try
                    {
                        await esperaCupoLargaDuracion;
                        semaforoLargaDuracion.Release();
                    }
                    catch (OperationCanceledException) when (cancelarEsperaCupo.IsCancellationRequested)
                    { }

                    await tareaEjecucion;
                    return;
                }

                try
                {
                    await esperaCupoLargaDuracion;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    await tareaEjecucion;
                    return;
                }

                if (token.IsCancellationRequested || tareaEjecucion.IsCompleted)
                {
                    semaforoLargaDuracion.Release();
                    await tareaEjecucion;
                    return;
                }

                cupoLargaDuracionAdquirido = true;
                _tareasLargaDuracion.TryAdd(tareaEjecucion, comando.Id);

                semaforoParalelismo.Release();
                cupoParalelismoLiberado = true;

                _logger.LogInformation(
                    "El comando {ComandoId} continúa como tarea de larga duración. Tareas largas activas: {TareasLargaDuracionActivas}/{MaxTareasLargaDuracion}",
                    comando.Id,
                    _tareasLargaDuracion.Count,
                    _maxTareasLargaDuracion);

                await tareaEjecucion;
            }
            finally
            {
                cancelarEsperaUmbral.Cancel();
                _tareasLargaDuracion.TryRemove(tareaEjecucion, out _);

                if (cupoLargaDuracionAdquirido)
                    semaforoLargaDuracion.Release();

                if (!cupoParalelismoLiberado)
                    semaforoParalelismo.Release();
            }
        }

        private async Task ProcesarComandoAsync(ComandoEnCola comandoEnCola, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Guid ejecucionId = Guid.NewGuid();
            OrigenEjecucionComandoTipo origen = OrigenEjecucionComandoTipo.Directo;
            string? codigoOrigen = null;
            long? agregadoId = null;
            bool ejecucionIniciada = false;
            ResultadoComando resultado;
            PayloadResultadoComando? payloadResultado = null;
            IServiceScope? scope = null;
            IAlmacenColaComandos? almacenColaComandos = null;

            try
            {
                try
                {
                    scope = _serviceScopeFactory.CreateScope();
                    almacenColaComandos = scope.ServiceProvider.GetRequiredService<IAlmacenColaComandos>();
                    IFactoriaAbstractaComandos<string, ResultadoComando> factoriaComandos = scope.ServiceProvider.GetRequiredService<IFactoriaAbstractaComandos<string, ResultadoComando>>();
                    IRegistroProcesadoresSerializacionResultadosComando?
                        registroProcesadoresSerializacionResultados =
                            scope.ServiceProvider.GetService<IRegistroProcesadoresSerializacionResultadosComando>();
                    IAlmacenamientoPayloadResultadoComando? almacenamientoPayload = scope.ServiceProvider.GetService<IAlmacenamientoPayloadResultadoComando>();

                    _logger.LogInformation(
                        "Procesando comando {ComandoId}: {RutaComando}",
                        comandoEnCola.Id,
                        comandoEnCola.RutaComando);

                    IEnumerable<ComandoEnCola> comandosProcesando = await almacenColaComandos.MarcarComandosProcesandoAsync(
                        new[] { comandoEnCola.Id },
                        token);

                    comandoEnCola = comandosProcesando.FirstOrDefault() ?? comandoEnCola;
                    ResultadoArgumentosLineaComando argumentos = ArgumentosLineaComando.Parsear(comandoEnCola.Argumentos);

                    if (argumentos.Data is not null
                        && !string.IsNullOrWhiteSpace(comandoEnCola.DatosDeComando))
                    {
                        throw new ErrorDeSintaxisExcepcion(
                            "El comando persistido contiene --data y DatosDeComando simultáneamente.");
                    }

                    (origen, codigoOrigen, agregadoId) = ObtenerMetadatosOrigen(argumentos.Parametros);
                    ejecucionIniciada = true;
                    NotificarEjecucionSinInterrumpir(
                        ejecucionId,
                        comandoEnCola,
                        NotificacionEjecucionComandoTipo.Iniciada,
                        origen,
                        codigoOrigen,
                        agregadoId,
                        null,
                        null);

                    string? data = argumentos.Data ?? comandoEnCola.DatosDeComando;
                    LineaComando lineaComando = ParsearLineaComando(
                        comandoEnCola,
                        argumentos.Parametros,
                        data);
                    var comando = factoriaComandos.Crear(lineaComando);

                    resultado = await comando.EjecutarAsync(lineaComando.Data ?? string.Empty, token);

                    stopwatch.Stop();
                    resultado.Duracion = stopwatch.Elapsed;
                    payloadResultado = await CrearPayloadResultadoAsync(
                        comandoEnCola.RutaComando,
                        comandoEnCola.Id,
                        resultado,
                        registroProcesadoresSerializacionResultados,
                        almacenamientoPayload,
                        token);

                    _logger.LogInformation("Comando {ComandoId} procesado. Exitoso: {Exitoso}, Duración: {Duracion}ms",
                        comandoEnCola.Id, resultado.Exitoso, resultado.Duracion.TotalMilliseconds);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    stopwatch.Stop();

                    if (ejecucionIniciada)
                    {
                        NotificarEjecucionSinInterrumpir(
                            ejecucionId,
                            comandoEnCola,
                            NotificacionEjecucionComandoTipo.Interrumpida,
                            origen,
                            codigoOrigen,
                            agregadoId,
                            stopwatch.Elapsed,
                            "La ejecución fue interrumpida por cancelación.");
                    }

                    _logger.LogWarning("Ejecución del comando {ComandoId} interrumpida por cancelación", comandoEnCola.Id);
                    return;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    resultado = ResultadoComando.Fallo(
                        $"Excepción durante la ejecución: {ex.Message}",
                        stopwatch.Elapsed);

                    _logger.LogError(ex, "Error procesando comando {ComandoId}", comandoEnCola.Id);
                }

                try
                {
                    if (almacenColaComandos is null)
                        throw new InvalidOperationException("No se pudo resolver el almacén de cola de comandos.");

                    await almacenColaComandos.MarcarComoProcesadoAsync(comandoEnCola.Id, resultado, payloadResultado, token);
                    NotificarEjecucionSinInterrumpir(
                        ejecucionId,
                        comandoEnCola,
                        resultado.Exitoso
                            ? NotificacionEjecucionComandoTipo.Completada
                            : NotificacionEjecucionComandoTipo.Fallida,
                        origen,
                        codigoOrigen,
                        agregadoId,
                        resultado.Duracion,
                        resultado.Exitoso ? null : resultado.MensajeError);
                    _colaComandosMemoria.CompletarResultado(comandoEnCola.Id, resultado);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    if (ejecucionIniciada)
                    {
                        NotificarEjecucionSinInterrumpir(
                            ejecucionId,
                            comandoEnCola,
                            NotificacionEjecucionComandoTipo.Interrumpida,
                            origen,
                            codigoOrigen,
                            agregadoId,
                            stopwatch.Elapsed,
                            "La persistencia del resultado fue interrumpida por cancelación.");
                    }

                    _logger.LogWarning("Persistencia del comando {ComandoId} interrumpida por cancelación", comandoEnCola.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al marcar comando {ComandoId} como procesado", comandoEnCola.Id);

                    if (ejecucionIniciada)
                    {
                        NotificarEjecucionSinInterrumpir(
                            ejecucionId,
                            comandoEnCola,
                            NotificacionEjecucionComandoTipo.ErrorPersistencia,
                            origen,
                            codigoOrigen,
                            agregadoId,
                            resultado.Duracion,
                            ex.Message);
                    }

                    _colaComandosMemoria.CompletarResultado(
                        comandoEnCola.Id,
                        ResultadoComando.Fallo($"Error al persistir resultado: {ex.Message}", resultado.Duracion));
                }
            }
            finally
            {
                scope?.Dispose();
                LimpiarTareasCompletadas();
            }
        }

        private static (
            OrigenEjecucionComandoTipo Origen,
            string? CodigoOrigen,
            long? AgregadoId) ObtenerMetadatosOrigen(IEnumerable<Parametro> argumentos)
        {
            if (!argumentos.Any())
                return (OrigenEjecucionComandoTipo.Directo, null, null);

            bool tieneOrigen = TryObtenerValorArgumento(argumentos, "--origen", out string? valorOrigen);

            if (!tieneOrigen)
                return (OrigenEjecucionComandoTipo.Directo, null, null);

            TryObtenerValorArgumento(argumentos, "--codigo", out string? codigoOrigen);
            bool tieneAgregado = TryObtenerValorArgumento(argumentos, "--agregado-id", out string? valorAgregado);
            long? agregadoId = null;

            if (tieneAgregado)
            {
                if (!long.TryParse(valorAgregado, out long agregadoParseado))
                    return (OrigenEjecucionComandoTipo.Desconocido, codigoOrigen, null);

                agregadoId = agregadoParseado;
            }

            if (string.IsNullOrWhiteSpace(codigoOrigen))
                return (OrigenEjecucionComandoTipo.Desconocido, codigoOrigen, agregadoId);

            if (string.Equals(valorOrigen, "evento", StringComparison.OrdinalIgnoreCase))
                return (OrigenEjecucionComandoTipo.Evento, codigoOrigen, agregadoId);

            if (string.Equals(valorOrigen, "disparador", StringComparison.OrdinalIgnoreCase))
                return (OrigenEjecucionComandoTipo.Disparador, codigoOrigen, agregadoId);

            return (OrigenEjecucionComandoTipo.Desconocido, codigoOrigen, agregadoId);
        }

        private static bool TryObtenerValorArgumento(
            IEnumerable<Parametro> argumentos,
            string nombre,
            out string? valor)
        {
            foreach (Parametro argumento in argumentos)
            {
                if (string.Equals(argumento.Nombre, nombre, StringComparison.Ordinal))
                {
                    valor = argumento.Valor;
                    return true;
                }
            }

            valor = null;
            return false;
        }

        private void NotificarEjecucionSinInterrumpir(
            Guid ejecucionId,
            ComandoEnCola comando,
            NotificacionEjecucionComandoTipo tipo,
            OrigenEjecucionComandoTipo origen,
            string? codigoOrigen,
            long? agregadoId,
            TimeSpan? duracion,
            string? error)
        {
            try
            {
                _publicadorNotificaciones.Notificar(
                    new NotificacionEjecucionComando(
                        ejecucionId,
                        comando.Id,
                        comando.RutaComando,
                        tipo,
                        origen,
                        codigoOrigen,
                        agregadoId,
                        DateTime.UtcNow,
                        duracion,
                        error));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No fue posible notificar el estado {TipoNotificacion} del comando {ComandoId}",
                    tipo,
                    comando.Id);
            }
        }

        private static LineaComando ParsearLineaComando(
            ComandoEnCola comandoEnCola,
            ICollection<Parametro> parametros,
            string? data)
        {
            string[] rutaPartes = comandoEnCola.RutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new LineaComando(rutaPartes, parametros, data);
        }

        private static async Task<PayloadResultadoComando?> CrearPayloadResultadoAsync(
            string rutaComando,
            long comandoId,
            ResultadoComando resultado,
            IRegistroProcesadoresSerializacionResultadosComando? registroProcesadoresSerializacionResultados,
            IAlmacenamientoPayloadResultadoComando? almacenamientoPayload,
            CancellationToken token)
        {
            if (!resultado.Exitoso
                || resultado.Salida is null
                || registroProcesadoresSerializacionResultados is null
                || almacenamientoPayload is null)
                return null;

            IProcesadorResultadoComando? procesador =
                registroProcesadoresSerializacionResultados.ObtenerPorRutaComando(rutaComando);

            if (procesador is null)
                return null;

            string? contenido = await procesador.SerializarAsync(resultado.Salida, token);

            if (contenido is null)
                return null;

            PayloadResultadoComando payload = new PayloadResultadoComando
            {
                Tipo = procesador.Tipo,
                Version = procesador.Version,
                Formato = procesador.Formato,
                Contenido = contenido
            };

            return await almacenamientoPayload.GuardarAsync(comandoId, payload, token);
        }

        private void LimpiarTareasCompletadas()
        {
            foreach (Task tarea in _tareasEnEjecucion.Keys)
            {
                if (tarea.IsCompleted)
                    _tareasEnEjecucion.TryRemove(tarea, out _);
            }
        }
    }
}
