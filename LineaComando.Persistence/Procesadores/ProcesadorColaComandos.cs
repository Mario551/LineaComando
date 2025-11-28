using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Stream;
using PER.Comandos.LineaComandos.Configuracion;

namespace PER.Comandos.LineaComandos.QueueCommand.Procesadores
{
    /// <summary>
    /// Procesador abstracto de cola de comandos.
    /// Lee comandos pendientes, los ejecuta usando FactoriaComandos y marca como procesados.
    /// </summary>
    public abstract class ProcesadorColaComandos<TRead, TWrite>
    {
        protected readonly IAlmacenColaComandos _almacenCola;
        protected readonly FactoriaComandos<TRead, TWrite> _factoria;
        protected readonly IConfiguracion _configuracion;
        protected readonly ILogger _logger;
        protected readonly Func<IStream<TRead, TWrite>> _crearStream;

        protected ProcesadorColaComandos(
            IAlmacenColaComandos almacenCola,
            FactoriaComandos<TRead, TWrite> factoria,
            IConfiguracion configuracion,
            ILogger logger,
            Func<IStream<TRead, TWrite>> crearStream)
        {
            _almacenCola = almacenCola;
            _factoria = factoria;
            _configuracion = configuracion;
            _logger = logger;
            _crearStream = crearStream;
        }

        /// <summary>
        /// Procesa un lote de comandos pendientes.
        /// </summary>
        public virtual async Task ProcesarLoteAsync(int tamanioLote = 50, CancellationToken token = default)
        {
            IEnumerable<ComandoEnCola> comandosPendientes = await _almacenCola.ObtenerComandosPendientesAsync(tamanioLote, token);

            foreach (var comandoEnCola in comandosPendientes)
            {
                if (token.IsCancellationRequested)
                    break;

                await ProcesarComandoAsync(comandoEnCola, token);
            }
        }

        /// <summary>
        /// Procesa un comando individual.
        /// </summary>
        protected virtual async Task ProcesarComandoAsync(ComandoEnCola comandoEnCola, CancellationToken token)
        {
            var cronometro = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Procesando comando {ComandoId}: {RutaComando}",
                    comandoEnCola.Id, comandoEnCola.RutaComando);

                // Parsear la línea de comando
                var lineaComando = new LineaComando(
                    comandoEnCola.RutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Concat(ParsearArgumentos(comandoEnCola.Argumentos))
                        .ToArray());

                // Crear el comando usando la factoría
                var comando = _factoria.Crear(lineaComando, _configuracion, _logger);

                // Ejecutar el comando
                var stream = _crearStream();
                await comando.EjecutarAsync(stream, token);

                cronometro.Stop();

                var resultado = ResultadoComando.Exito(
                    ObtenerSalidaStream(stream),
                    cronometro.Elapsed);

                await _almacenCola.MarcarComoProcesadoAsync(comandoEnCola.Id, resultado, token);

                _logger.LogInformation("Comando {ComandoId} procesado exitosamente en {Duracion}ms",
                    comandoEnCola.Id, cronometro.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                cronometro.Stop();

                _logger.LogError(ex, "Error procesando comando {ComandoId}: {Error}",
                    comandoEnCola.Id, ex.Message);

                if (DebeReintentar(comandoEnCola, ex))
                {
                    await _almacenCola.MarcarParaReintentoAsync(comandoEnCola.Id, ex.Message, token);
                }
                else
                {
                    var resultado = ResultadoComando.Fallo(ex.Message, cronometro.Elapsed);
                    await _almacenCola.MarcarComoProcesadoAsync(comandoEnCola.Id, resultado, token);
                }
            }
        }

        /// <summary>
        /// Parsea los argumentos de texto a array.
        /// </summary>
        protected virtual IEnumerable<string> ParsearArgumentos(string argumentos)
        {
            if (string.IsNullOrWhiteSpace(argumentos))
                return Enumerable.Empty<string>();

            // Implementación básica, puede ser sobrescrita para manejo más sofisticado
            return argumentos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Obtiene la salida del stream (si aplica).
        /// </summary>
        protected virtual string? ObtenerSalidaStream(IStream<TRead, TWrite> stream)
        {
            return null;
        }

        /// <summary>
        /// Determina si el comando debe reintentarse.
        /// </summary>
        protected virtual bool DebeReintentar(ComandoEnCola comando, Exception ex)
        {
            // Por defecto, reintentar hasta 3 veces
            return comando.Intentos < 3;
        }
    }
}
