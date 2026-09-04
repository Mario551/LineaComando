using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Excepcion;

namespace PER.Comandos.LineaComandos.FactoriaComandos
{
    public class FactoriaAbstractaComandos<TRead, TWrite> : IFactoriaAbstractaComandos<TRead, TWrite>
    {
        private readonly IDictionary<string, IFactoriaComandos<TRead, TWrite>> _factorias;

        public FactoriaAbstractaComandos(IEnumerable<IFactoriaComandos<TRead, TWrite>> factorias)
        {
            ArgumentNullException.ThrowIfNull(factorias);

            _factorias = new Dictionary<string, IFactoriaComandos<TRead, TWrite>>(StringComparer.Ordinal);

            foreach (IFactoriaComandos<TRead, TWrite> factoria in factorias)
                Add(factoria);
        }

        public void Add(IFactoriaComandos<TRead, TWrite> factoria)
        {
            ArgumentNullException.ThrowIfNull(factoria);

            if (!_factorias.TryAdd(factoria.Nombre, factoria))
                throw new InvalidOperationException($"Ya existe una factoría de comandos con el nombre '{factoria.Nombre}'.");
        }

        public IFactoriaComandos<TRead, TWrite> Get(string nombre)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

            if (!_factorias.TryGetValue(nombre, out IFactoriaComandos<TRead, TWrite>? factoria))
                throw new NoEncontradoExcepcion($"factoría de comandos '{nombre}' no encontrada");

            return factoria;
        }

        public IComando<TRead, TWrite> Crear(LineaComando lineaComando)
        {
            ArgumentNullException.ThrowIfNull(lineaComando);

            string[] ruta = lineaComando.Ruta.ToArray();
            if (ruta.Length < 2)
                throw new ErrorDeSintaxisExcepcion("La ruta debe contener el nombre de una factoría y un comando.");

            IFactoriaComandos<TRead, TWrite> factoria = Get(ruta[0]);
            LineaComando lineaComandoFactoria = new(
                ruta.Skip(1).ToArray(),
                lineaComando.Parametros,
                lineaComando.Data);

            return factoria.Crear(lineaComandoFactoria);
        }
    }
}
