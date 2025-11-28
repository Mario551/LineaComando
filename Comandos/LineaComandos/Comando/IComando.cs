using PER.Comandos.LineaComandos.Stream;

namespace PER.Comandos.LineaComandos.Comando
{
    /// <summary>
    /// Interfaz base para comandos.
    /// </summary>
    public interface IComando<TRead, TWrite>
    {
        /// <summary>
        /// Configura el middleware a ejecutar antes del comando.
        /// </summary>
        void EmpezarCon(Func<CancellationToken, Task> comenzar);

        /// <summary>
        /// Configura el middleware a ejecutar después del comando.
        /// </summary>
        void FinalizarCon(Func<CancellationToken, Task> finalizar);

        /// <summary>
        /// Ejecuta el comando de forma asíncrona.
        /// </summary>
        Task EjecutarAsync(IStream<TRead, TWrite> stream, CancellationToken token = default);
    }
}
