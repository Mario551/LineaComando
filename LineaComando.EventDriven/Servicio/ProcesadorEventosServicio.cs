using System.Text;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace PER.Comandos.LineaComandos.EventDriven.Servicio
{
    /// <summary>
    /// Procesador de eventos del Outbox.
    /// Lee eventos pendientes, encola los comandos asociados a handlers y marca como procesados.
    /// </summary>
    public class ProcesadorEventos
    {
        private readonly IColaEventos _almacenOutbox;
        private readonly IRegistroManejadores _registroManejadores;
        private readonly IColaComandosMemoria _colaComandosMemoria;
        private readonly ILogger<ProcesadorEventos> _logger;

        public ProcesadorEventos(
            IColaEventos almacenOutbox,
            IRegistroManejadores registroManejadores,
            IColaComandosMemoria colaComandosMemoria,
            ILogger<ProcesadorEventos> logger)
        {
            _almacenOutbox = almacenOutbox ?? throw new ArgumentNullException(nameof(almacenOutbox));
            _registroManejadores = registroManejadores ?? throw new ArgumentNullException(nameof(registroManejadores));
            _colaComandosMemoria = colaComandosMemoria ?? throw new ArgumentNullException(nameof(colaComandosMemoria));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Procesa un lote de eventos pendientes.
        /// </summary>
        public virtual async Task ProcesarLoteAsync(int tamanioLote = 50, CancellationToken token = default)
        {
            var eventosPendientes = await _almacenOutbox.ObtenerEventosPendientesAsync(tamanioLote, token);

            foreach (var evento in eventosPendientes)
            {
                if (token.IsCancellationRequested)
                    break;

                await ProcesarEventoAsync(evento, token);
            }
        }

        /// <summary>
        /// Procesa un evento individual.
        /// </summary>
        public virtual async Task ProcesarEventoAsync(EventoOutbox evento, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Procesando evento {Id} de tipo {CodigoTipoEvento}",
                    evento.Id, evento.CodigoTipoEvento);

                // Obtener handlers configurados para este tipo de evento
                var configuraciones = await _registroManejadores.ObtenerManejadoresParaEventoAsync(
                    evento.CodigoTipoEvento, token);

                var configuracionesActivas = configuraciones
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Prioridad)
                    .ToList();

                if (!configuracionesActivas.Any())
                {
                    _logger.LogDebug("No hay handlers configurados para el evento {TipoEvento}",
                        evento.CodigoTipoEvento);
                    await _almacenOutbox.MarcarComoProcesadoAsync(evento.Id, token);
                    return;
                }

                // Ejecutar cada handler
                foreach (var config in configuracionesActivas)
                {
                    if (token.IsCancellationRequested)
                        break;

                    await EjecutarHandlerAsync(evento, config, token);
                }

                await _almacenOutbox.MarcarComoProcesadoAsync(evento.Id, token);

                _logger.LogInformation("Evento {EventoId} procesado exitosamente", evento.Id);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.LogWarning("Procesamiento de evento {EventoId} cancelado.", evento.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando evento {EventoId}: {Error}",
                    evento.Id, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Encola el comando asociado al handler.
        /// </summary>
        protected virtual async Task EjecutarHandlerAsync(
            EventoOutbox evento,
            ConfiguracionManejador config,
            CancellationToken token)
        {
            _logger.LogDebug("Encolando comando para handler {ManejadorId} en respuesta a evento {EventoId}",
                config.IDManejador, evento.Id);

            StringBuilder sbArgumentos = new StringBuilder();
            sbArgumentos.Append("--origen=evento");
            sbArgumentos.Append(" --codigo=" + evento.CodigoTipoEvento);
            if (evento.AgregadoId != null)
                sbArgumentos.Append(" --agregado-id=" + evento.AgregadoId);
            if (!string.IsNullOrEmpty(config.ArgumentosComando))
                sbArgumentos.Append(" " + config.ArgumentosComando);
            string argumentos = sbArgumentos.ToString();

            SolicitudComando solicitud = new SolicitudComando
            {
                RutaComando = config.RutaComando,
                Argumentos = argumentos,
                DatosDeComando = evento.DatosEvento
            };

            await _colaComandosMemoria.EncolarAsync(solicitud, token);

            _logger.LogInformation("Comando encolado: {RutaComando} para evento {EventoId}",
                config.RutaComando, evento.Id);
        }
    }
}
