using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace PER.Comandos.LineaComandos.Registro
{
    /// <summary>
    /// Leer comandos registrados y construir FactoriaComandos.
    /// </summary>
    public interface IRegistroComandos<TRead, TWrite>
    {
        IDictionary<string, MetadatosComando> ComandosRegistrados { get; }

        /// <summary>
        /// Obtiene todos los comandos registrados activos.
        /// </summary>
        Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(CancellationToken token = default);

        /// <summary>
        /// Construye la factoría con los comandos registrados.
        /// </summary>
        Task ConstruirFactoriaAsync(FactoriaComandos<TRead, TWrite> factoria, CancellationToken token = default);

        /// <summary>
        /// Registra un nuevo comando.
        /// </summary>
        Task RegistrarComandoAsync(MetadatosComando metadatos, IComandoCreador<TRead, TWrite> comandoCreador, CancellationToken token = default);

        /// <summary>
        /// Elimina el registro de un comando.
        /// </summary>
        Task EliminarRegistroComandoAsync(string rutaComando, CancellationToken token = default);
    }
}
