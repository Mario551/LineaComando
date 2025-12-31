using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Stream;
using PER.Comandos.LineaComandos.Configuracion;

namespace PER.Comandos.LineaComandos.Cola.Procesadores
{
    public class ProcesadorColaComandos
    {
        private readonly IAlmacenColaComandos _almacenColaComandos;
        private readonly IFactoriaComandos<string, ResultadoComando> _factoriaComandos;
        private readonly IConfiguracion _configuracion;
        private readonly ILogger _logger;
        private readonly int _maxParalelismo;
        private readonly TimeSpan _tiempoRefresco;
        private readonly ConcurrentBag<Task> _tareasEnEjecucion;

        public ProcesadorColaComandos(
            IAlmacenColaComandos almacenColaComandos,
            IFactoriaComandos<string, ResultadoComando> factoriaComandos,
            IConfiguracion configuracion,
            ILogger logger,
            int maxParalelismo,
            TimeSpan tiempoRefresco)
        {
            if (maxParalelismo <= 0)
                throw new ArgumentException("El máximo paralelismo debe ser mayor a cero", nameof(maxParalelismo));

            if (tiempoRefresco.TotalMilliseconds <= 0)
                throw new ArgumentException("El tiempo de refresco debe ser mayor a cero", nameof(tiempoRefresco));

            _almacenColaComandos = almacenColaComandos ?? throw new ArgumentNullException(nameof(almacenColaComandos));
            _factoriaComandos = factoriaComandos ?? throw new ArgumentNullException(nameof(factoriaComandos));
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxParalelismo = maxParalelismo;
            _tiempoRefresco = tiempoRefresco;
            _tareasEnEjecucion = new ConcurrentBag<Task>();
        }

        public async Task StartAsync(CancellationToken token)
        {
            _logger.LogInformation("ProcesadorColaComandos iniciado con paralelismo máximo de {MaxParalelismo}", _maxParalelismo);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var comandosPendientes = await _almacenColaComandos.ObtenerComandosPendientesAsync(50, token);

                    foreach (var comando in comandosPendientes)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        while (_tareasEnEjecucion.Count >= _maxParalelismo)
                        {
                            await Task.WhenAny(_tareasEnEjecucion);
                            LimpiarTareasCompletadas();
                        }

                        Task tarea = ProcesarComandoAsync(comando, token);
                        _tareasEnEjecucion.Add(tarea);
                    }

                    await Task.Delay(_tiempoRefresco, token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ProcesadorColaComandos cancelado");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el bucle principal del ProcesadorColaComandos");
                }
            }

            if (_tareasEnEjecucion.Any())
            {
                _logger.LogInformation("Esperando que terminen {Count} tareas pendientes", _tareasEnEjecucion.Count);
                await Task.WhenAll(_tareasEnEjecucion);
            }

            _logger.LogInformation("ProcesadorColaComandos finalizado");
        }

        private async Task ProcesarComandoAsync(ComandoEnCola comandoEnCola, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            ResultadoComando resultado;

            try
            {
                _logger.LogInformation("Procesando comando {ComandoId}: {RutaComando} {Argumentos}",
                    comandoEnCola.Id, comandoEnCola.RutaComando, comandoEnCola.Argumentos);

                var lineaComando = ParsearLineaComando(comandoEnCola);
                var comando = _factoriaComandos.Crear(lineaComando);
                var stream = new StreamEnMemoria<string, ResultadoComando>(comandoEnCola.DatosDeComando ?? string.Empty);

                await comando.EjecutarAsync(stream, token);

                resultado = stream.ObtenerResultado() ?? ResultadoComando.Fallo(
                    "El comando no escribió ningún resultado",
                    stopwatch.Elapsed);

                resultado.Duracion = stopwatch.Elapsed;

                _logger.LogInformation("Comando {ComandoId} procesado. Exitoso: {Exitoso}, Duración: {Duracion}ms",
                    comandoEnCola.Id, resultado.Exitoso, resultado.Duracion.TotalMilliseconds);
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
                await _almacenColaComandos.MarcarComoProcesadoAsync(comandoEnCola.Id, resultado, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar comando {ComandoId} como procesado", comandoEnCola.Id);
            }
            finally
            {
                LimpiarTareasCompletadas();
            }
        }

        private LineaComando ParsearLineaComando(ComandoEnCola comandoEnCola)
        {
            var partes = new List<string>();

            var rutaPartes = comandoEnCola.RutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            partes.AddRange(rutaPartes);

            if (!string.IsNullOrWhiteSpace(comandoEnCola.Argumentos))
            {
                var argumentosPartes = comandoEnCola.Argumentos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                partes.AddRange(argumentosPartes);
            }

            return new LineaComando(partes);
        }

        private void LimpiarTareasCompletadas()
        {
            var tareasActivas = _tareasEnEjecucion.Where(t => !t.IsCompleted).ToList();

            while (_tareasEnEjecucion.TryTake(out _)) { }

            foreach (var tarea in tareasActivas)
            {
                _tareasEnEjecucion.Add(tarea);
            }
        }
    }
}
