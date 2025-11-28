using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.EventSourcing.Handler;
using PER.Comandos.LineaComandos.EventSourcing.Outbox;
using PER.Comandos.LineaComandos.EventSourcing.Registro;

namespace PER.Comandos.LineaComandos.EventSourcing.Procesadores
namespace PER.Comandos.LineaComandos.QueueCommand.Procesadores
{
    /// <summary>
    /// Procesador de eventos del Outbox.
    /// Lee eventos pendientes, ejecuta los handlers correspondientes y marca como procesados.
    /// </summary>
    public class ProcesadorEventos
    {
        private readonly IAlmacenOutbox _almacenOutbox;
        private readonly IRegistroManejadores _registroManejadores;
        private readonly ILogger _logger;
        private readonly IServiceProvider? _serviceProvider;

        public ProcesadorEventos(
            IAlmacenOutbox almacenOutbox,
            IRegistroManejadores registroManejadores,
            ILogger logger,
            IServiceProvider? serviceProvider = null)
        {
            _almacenOutbox = almacenOutbox;
            _registroManejadores = registroManejadores;
            _logger = logger;
            _serviceProvider = serviceProvider;
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
        protected virtual async Task ProcesarEventoAsync(EventoOutbox evento, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Procesando evento {EventoId} de tipo {TipoEvento}",
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

                    // Verificar filtros
                    if (!CumpleFiltros(evento, config))
                        continue;

                    await EjecutarHandlerAsync(evento, config, token);
                }

                await _almacenOutbox.MarcarComoProcesadoAsync(evento.Id, token);

                _logger.LogInformation("Evento {EventoId} procesado exitosamente", evento.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando evento {EventoId}: {Error}",
                    evento.Id, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Ejecuta un handler específico.
        /// </summary>
        protected virtual async Task EjecutarHandlerAsync(
            EventoOutbox evento,
            ConfiguracionManejador config,
            CancellationToken token)
        {
            _logger.LogDebug("Ejecutando handler {ManejadorId} para evento {EventoId}",
                config.ManejadorId, evento.Id);

            // Aquí el usuario implementaría la lógica de instanciación del handler
            // usando reflexión y el service provider
            await Task.CompletedTask;
        }

        /// <summary>
        /// Verifica si el evento cumple con los filtros del handler.
        /// </summary>
        protected virtual bool CumpleFiltros(EventoOutbox evento, ConfiguracionManejador config)
        {
            if (string.IsNullOrEmpty(config.CondicionesFiltro))
                return true;

            try
            {
                var filtros = JsonSerializer.Deserialize<Dictionary<string, object>>(config.CondicionesFiltro);
                var datosEvento = JsonSerializer.Deserialize<Dictionary<string, object>>(evento.DatosEvento);

                if (filtros == null || datosEvento == null)
                    return true;

                foreach (var filtro in filtros)
                {
                    if (!datosEvento.TryGetValue(filtro.Key, out var valor))
                        return false;

                    if (!valor?.ToString()?.Equals(filtro.Value?.ToString(), StringComparison.OrdinalIgnoreCase) ?? false)
                        return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}
