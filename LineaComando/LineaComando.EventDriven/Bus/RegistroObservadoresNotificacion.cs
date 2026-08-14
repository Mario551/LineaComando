namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    internal sealed class RegistroObservadoresNotificacion<TNotificacion>
    {
        private readonly object _sincronizacion = new object();
        private readonly Dictionary<string, HashSet<ObservadorNotificacionEnMemoria<TNotificacion>>> _observadores =
            new Dictionary<string, HashSet<ObservadorNotificacionEnMemoria<TNotificacion>>>(StringComparer.Ordinal);

        public TObservador Suscribir<TObservador>(
            string clave,
            Func<Action<ObservadorNotificacionEnMemoria<TNotificacion>>, TObservador> crearObservador)
            where TObservador : ObservadorNotificacionEnMemoria<TNotificacion>
        {
            TObservador observador = crearObservador(
                observadorDispuesto => Eliminar(clave, observadorDispuesto));

            lock (_sincronizacion)
            {
                if (!_observadores.TryGetValue(
                        clave,
                        out HashSet<ObservadorNotificacionEnMemoria<TNotificacion>>? observadoresClave))
                {
                    observadoresClave = new HashSet<ObservadorNotificacionEnMemoria<TNotificacion>>();
                    _observadores.Add(clave, observadoresClave);
                }

                observadoresClave.Add(observador);
            }

            return observador;
        }

        public void Notificar(string clave, TNotificacion notificacion)
        {
            ObservadorNotificacionEnMemoria<TNotificacion>[] observadores;

            lock (_sincronizacion)
            {
                if (!_observadores.TryGetValue(
                        clave,
                        out HashSet<ObservadorNotificacionEnMemoria<TNotificacion>>? observadoresClave))
                {
                    return;
                }

                observadores = observadoresClave.ToArray();
            }

            foreach (ObservadorNotificacionEnMemoria<TNotificacion> observador in observadores)
                observador.Notificar(notificacion);
        }

        private void Eliminar(
            string clave,
            ObservadorNotificacionEnMemoria<TNotificacion> observador)
        {
            lock (_sincronizacion)
            {
                if (!_observadores.TryGetValue(
                        clave,
                        out HashSet<ObservadorNotificacionEnMemoria<TNotificacion>>? observadoresClave))
                {
                    return;
                }

                observadoresClave.Remove(observador);

                if (observadoresClave.Count == 0)
                    _observadores.Remove(clave);
            }
        }
    }
}
