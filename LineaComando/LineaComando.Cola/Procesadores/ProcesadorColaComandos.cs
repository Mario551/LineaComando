using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Notificaciones;
using PER.Comandos.LineaComandos.Cola.Resultados;
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
        private readonly ConcurrentBag<Task> _tareasEnEjecucion;

        public ProcesadorColaComandos(
            IServiceScopeFactory serviceScopeFactory,
            IColaComandosMemoria colaComandosMemoria,
            IPublicadorNotificacionEjecucionComandos publicadorNotificaciones,
            int maxParalelismo,
            ILogger<ProcesadorColaComandos> logger)
        {
            if (maxParalelismo <= 0)
                throw new ArgumentException("El máximo paralelismo debe ser mayor a cero", nameof(maxParalelismo));

            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _colaComandosMemoria = colaComandosMemoria ?? throw new ArgumentNullException(nameof(colaComandosMemoria));
            _publicadorNotificaciones = publicadorNotificaciones ?? throw new ArgumentNullException(nameof(publicadorNotificaciones));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxParalelismo = maxParalelismo;
            _tareasEnEjecucion = new ConcurrentBag<Task>();
        }

        public async Task StartAsync(CancellationToken token)
        {
            _logger.LogInformation("ProcesadorColaComandos iniciado con paralelismo máximo de {MaxParalelismo}", _maxParalelismo);

            await CargarPendientesConReintentoAsync(token);

            using SemaphoreSlim semaforoParalelismo = new(_maxParalelismo, _maxParalelismo);

            try
            {
                await foreach (ComandoEnCola comando in _colaComandosMemoria.LeerAsync(token))
                {
                    LimpiarTareasCompletadas();

                    await semaforoParalelismo.WaitAsync(token);

                    Task tarea = ProcesarComandoConLiberacionAsync(comando, semaforoParalelismo, token);

                    _tareasEnEjecucion.Add(tarea);
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

            if (_tareasEnEjecucion.Any())
            {
                _logger.LogInformation("Esperando que terminen {Count} tareas pendientes", _tareasEnEjecucion.Count);
                try
                {
                    await Task.WhenAll(_tareasEnEjecucion);
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
            CancellationToken token)
        {
            try
            {
                await ProcesarComandoAsync(comando, token);
            }
            finally
            {
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
                    IFactoriaComandos<string, ResultadoComando> factoriaComandos = scope.ServiceProvider.GetRequiredService<IFactoriaComandos<string, ResultadoComando>>();
                    IRegistroProcesadoresSerializacionResultadosComando?
                        registroProcesadoresSerializacionResultados =
                            scope.ServiceProvider.GetService<IRegistroProcesadoresSerializacionResultadosComando>();
                    IAlmacenamientoPayloadResultadoComando? almacenamientoPayload = scope.ServiceProvider.GetService<IAlmacenamientoPayloadResultadoComando>();

                    _logger.LogInformation("Procesando comando {ComandoId}: {RutaComando} {Argumentos}",
                        comandoEnCola.Id, comandoEnCola.RutaComando, comandoEnCola.Argumentos);

                    IEnumerable<ComandoEnCola> comandosProcesando = await almacenColaComandos.MarcarComandosProcesandoAsync(
                        new[] { comandoEnCola.Id },
                        token);

                    comandoEnCola = comandosProcesando.FirstOrDefault() ?? comandoEnCola;
                    (origen, codigoOrigen, agregadoId) = ObtenerMetadatosOrigen(comandoEnCola.Argumentos);
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

                    LineaComando lineaComando = ParsearLineaComando(comandoEnCola);
                    var comando = factoriaComandos.Crear(lineaComando);

                    resultado = await comando.EjecutarAsync(comandoEnCola.DatosDeComando ?? string.Empty, token);

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
            long? AgregadoId) ObtenerMetadatosOrigen(string? argumentos)
        {
            if (string.IsNullOrWhiteSpace(argumentos))
                return (OrigenEjecucionComandoTipo.Directo, null, null);

            string[] partes = argumentos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool tieneOrigen = TryObtenerValorArgumento(partes, "--origen", out string? valorOrigen);

            if (!tieneOrigen)
                return (OrigenEjecucionComandoTipo.Directo, null, null);

            TryObtenerValorArgumento(partes, "--codigo", out string? codigoOrigen);
            bool tieneAgregado = TryObtenerValorArgumento(partes, "--agregado-id", out string? valorAgregado);
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
            IEnumerable<string> argumentos,
            string nombre,
            out string? valor)
        {
            string prefijo = $"{nombre}=";

            foreach (string argumento in argumentos)
            {
                if (string.Equals(argumento, nombre, StringComparison.Ordinal))
                {
                    valor = null;
                    return true;
                }

                if (argumento.StartsWith(prefijo, StringComparison.Ordinal))
                {
                    valor = argumento[prefijo.Length..];
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

        private LineaComando ParsearLineaComando(ComandoEnCola comandoEnCola)
        {
            List<string> partes = new List<string>();

            string[] rutaPartes = comandoEnCola.RutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            partes.AddRange(rutaPartes);

            if (!string.IsNullOrWhiteSpace(comandoEnCola.Argumentos))
            {
                string[] argumentosPartes = comandoEnCola.Argumentos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                partes.AddRange(argumentosPartes);
            }

            return new LineaComando(partes);
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
            List<Task> tareasActivas = _tareasEnEjecucion.Where(t => !t.IsCompleted).ToList();

            while (_tareasEnEjecucion.TryTake(out _)) { }

            foreach (var tarea in tareasActivas)
            {
                _tareasEnEjecucion.Add(tarea);
            }
        }
    }
}
