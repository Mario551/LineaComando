using System.Collections.Concurrent;

namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public sealed class RegistroProcesadoresResultadoComando : IRegistroProcesadoresResultadoComando
    {
        private readonly ConcurrentDictionary<string, IProcesadorResultadoComando> _procesadoresPorRuta;
        private readonly ConcurrentDictionary<string, IProcesadorResultadoComando> _procesadoresPorTipoVersion;

        public RegistroProcesadoresResultadoComando()
        {
            _procesadoresPorRuta = new ConcurrentDictionary<string, IProcesadorResultadoComando>();
            _procesadoresPorTipoVersion = new ConcurrentDictionary<string, IProcesadorResultadoComando>();
        }

        public void Registrar(string rutaComando, IProcesadorResultadoComando procesador)
        {
            if (string.IsNullOrWhiteSpace(rutaComando))
                throw new ArgumentException("La ruta del comando no puede estar vacía.", nameof(rutaComando));

            if (procesador is null)
                throw new ArgumentNullException(nameof(procesador));

            if (string.IsNullOrWhiteSpace(procesador.Tipo))
                throw new ArgumentException("El tipo del procesador de resultado no puede estar vacío.", nameof(procesador));

            if (procesador.Version <= 0)
                throw new ArgumentException("La versión del procesador de resultado debe ser mayor a cero.", nameof(procesador));

            if (string.IsNullOrWhiteSpace(procesador.Formato))
                throw new ArgumentException("El formato del procesador de resultado no puede estar vacío.", nameof(procesador));

            _procesadoresPorRuta[rutaComando] = procesador;
            _procesadoresPorTipoVersion[CrearLlave(procesador.Tipo, procesador.Version)] = procesador;
        }

        public IProcesadorResultadoComando? ObtenerPorRutaComando(string rutaComando)
        {
            return _procesadoresPorRuta.TryGetValue(rutaComando, out IProcesadorResultadoComando? procesador)
                ? procesador
                : null;
        }

        public IProcesadorResultadoComando? ObtenerPorTipoVersion(string tipo, int version)
        {
            return _procesadoresPorTipoVersion.TryGetValue(CrearLlave(tipo, version), out IProcesadorResultadoComando? procesador)
                ? procesador
                : null;
        }

        private static string CrearLlave(string tipo, int version)
        {
            return $"{tipo}:{version}";
        }
    }
}
