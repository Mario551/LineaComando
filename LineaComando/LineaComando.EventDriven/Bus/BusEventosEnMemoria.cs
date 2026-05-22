using System.Collections.Concurrent;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    /// <summary>
    /// Implementación por defecto del bus de eventos en memoria.
    /// </summary>
    public class BusEventosEnMemoria : IBusEventos
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _suscripciones = new();
        private readonly object _lock = new();

        public async Task PublicarAsync<TEvento>(TEvento evento, CancellationToken token = default) where TEvento : class
        {
            var tipoEvento = typeof(TEvento);

            if (!_suscripciones.TryGetValue(tipoEvento, out var manejadores))
                return;

            List<Delegate> copiaManejadores;
            lock (_lock)
            {
                copiaManejadores = manejadores.ToList();
            }

            foreach (var manejador in copiaManejadores)
            {
                if (token.IsCancellationRequested)
                    break;

                if (manejador is Func<TEvento, CancellationToken, Task> handler)
                {
                    await handler(evento, token);
                }
            }
        }

        public void Suscribir<TEvento>(Func<TEvento, CancellationToken, Task> manejador) where TEvento : class
        {
            var tipoEvento = typeof(TEvento);

            lock (_lock)
            {
                var manejadores = _suscripciones.GetOrAdd(tipoEvento, _ => new List<Delegate>());
                manejadores.Add(manejador);
            }
        }

        public void CancelarSuscripcion<TEvento>(Func<TEvento, CancellationToken, Task> manejador) where TEvento : class
        {
            var tipoEvento = typeof(TEvento);

            lock (_lock)
            {
                if (_suscripciones.TryGetValue(tipoEvento, out var manejadores))
                {
                    manejadores.Remove(manejador);
                }
            }
        }
    }
}
