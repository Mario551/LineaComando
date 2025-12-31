using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.LineaComandos.Stream;

namespace PER.Comandos.LineaComandos.Comando
{
    /// <summary>
    /// Clase base abstracta para comandos.
    /// Provee funcionalidad de middleware (EmpezarCon/FinalizarCon) y soporte opcional para eventos.
    /// </summary>
    public abstract class ComandoBase<TRead, TWrite> : IComando<TRead, TWrite>
    {
        private Func<CancellationToken, Task>? _comenzar;
        private Func<CancellationToken, Task>? _finalizar;

        /// <summary>
        /// Configura el middleware a ejecutar antes del comando.
        /// </summary>
        public void EmpezarCon(Func<CancellationToken, Task> comenzar)
            => _comenzar = comenzar;

        /// <summary>
        /// Configura el middleware a ejecutar después del comando.
        /// </summary>
        public void FinalizarCon(Func<CancellationToken, Task> finalizar)
            => _finalizar = finalizar;

        /// <summary>
        /// Ejecuta el middleware de inicio.
        /// </summary>
        protected async Task EmpezarAsync(CancellationToken token = default)
        {
            if (_comenzar == null)
                return;

            await _comenzar(token);
        }

        /// <summary>
        /// Ejecuta el middleware de finalización.
        /// </summary>
        protected async Task FinalizarAsync(CancellationToken token = default)
        {
            if (_finalizar == null)
                return;

            await _finalizar(token);
        }

        /// <summary>
        /// Prepara el comando con los parámetros.
        /// </summary>
        public abstract void Preparar(ICollection<Parametro> parametros);

        /// <summary>
        /// Ejecuta el comando de forma asíncrona.
        /// </summary>
        public abstract Task EjecutarAsync(IStream<TRead, TWrite> stream, CancellationToken token = default);
    }
}
