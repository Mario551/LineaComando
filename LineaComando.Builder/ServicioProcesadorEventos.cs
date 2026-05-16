using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.EventDriven.Colas;
using PER.Comandos.LineaComandos.EventDriven.Outbox;
using PER.Comandos.LineaComandos.EventDriven.Servicio;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioProcesadorEventos : BackgroundService
    {
        private const int MaxReintentosEvento = 3;
        private static readonly TimeSpan EsperaReintentoEvento = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan EsperaReintentoCarga = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IColaEventosMemoria _colaEventosMemoria;
        private readonly ILogger<ServicioProcesadorEventos> _logger;

        public ServicioProcesadorEventos(
            IServiceScopeFactory scopeFactory,
            IColaEventosMemoria colaEventosMemoria,
            ILogger<ServicioProcesadorEventos> logger)
        {
            _scopeFactory = scopeFactory;
            _colaEventosMemoria = colaEventosMemoria;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await CargarPendientesConReintentoAsync(stoppingToken);

                Dictionary<long, int> intentosEventos = new Dictionary<long, int>();

                await foreach (EventoOutbox evento in _colaEventosMemoria.LeerAsync(stoppingToken))
                {
                    await ProcesarEventoAsync(evento, intentosEventos, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("ServicioProcesadorEventos cancelado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ServicioProcesadorEventos finalizó por un error no controlado.");
            }
        }

        private async Task CargarPendientesConReintentoAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _colaEventosMemoria.CargarPendientesDesdeBaseDatosAsync(token);
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
                        "Error cargando eventos pendientes desde base de datos. Se reintentará en {EsperaTotalSegundos} segundos.",
                        EsperaReintentoCarga.TotalSeconds);

                    await Task.Delay(EsperaReintentoCarga, token);
                }
            }
        }

        private async Task ProcesarEventoAsync(
            EventoOutbox evento,
            Dictionary<long, int> intentosEventos,
            CancellationToken token)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ProcesadorEventos procesador = scope.ServiceProvider.GetRequiredService<ProcesadorEventos>();

                await procesador.ProcesarEventoAsync(evento, token);
                intentosEventos.Remove(evento.Id);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                int intento = intentosEventos.TryGetValue(evento.Id, out int intentoActual)
                    ? intentoActual + 1
                    : 1;

                if (intento > MaxReintentosEvento)
                {
                    intentosEventos.Remove(evento.Id);
                    _logger.LogError(
                        ex,
                        "Evento {EventoId} agotó {MaxReintentosEvento} reintentos y seguirá pendiente en base de datos.",
                        evento.Id,
                        MaxReintentosEvento);
                    return;
                }

                intentosEventos[evento.Id] = intento;
                _logger.LogWarning(
                    ex,
                    "Error procesando evento {EventoId}. Reintento {Intento}/{MaxReintentosEvento} en {EsperaTotalSegundos} segundos.",
                    evento.Id,
                    intento,
                    MaxReintentosEvento,
                    EsperaReintentoEvento.TotalSeconds);

                await Task.Delay(EsperaReintentoEvento, token);
                try
                {
                    await _colaEventosMemoria.EncolarAsync(evento, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception reencolarEx)
                {
                    intentosEventos.Remove(evento.Id);
                    _logger.LogError(
                        reencolarEx,
                        "No se pudo reencolar evento {EventoId}. El evento seguirá pendiente en base de datos.",
                        evento.Id);
                }
            }
        }
    }
}
