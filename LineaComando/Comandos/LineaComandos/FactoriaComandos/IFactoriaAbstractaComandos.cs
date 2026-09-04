using PER.Comandos.LineaComandos.Comando;

namespace PER.Comandos.LineaComandos.FactoriaComandos
{
    /// <summary>
    /// Resuelve una factoría de comandos usando la primera palabra de la ruta.
    /// </summary>
    public interface IFactoriaAbstractaComandos<TRead, TWrite>
    {
        void Add(IFactoriaComandos<TRead, TWrite> factoria);

        IFactoriaComandos<TRead, TWrite> Get(string nombre);

        IComando<TRead, TWrite> Crear(LineaComando lineaComando);
    }
}
