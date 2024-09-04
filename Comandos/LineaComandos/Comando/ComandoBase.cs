using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.LineaComandos.Stream;

namespace PER.Comandos.LineaComandos.Comando
{
    public abstract class ComandoBase<TRead, TWrite> : IComando<TRead, TWrite>
    {
        private Func<CancellationToken, Task>? _comenzar;
        private Func<CancellationToken, Task>? _finalizar;

        public void EmpezarCon(Func<CancellationToken, Task> comenzar)
            => _comenzar = comenzar;

        public void FinalizarCon(Func<CancellationToken, Task> finalizar)
            => _finalizar = finalizar;

        protected async Task EmpezarAsync(CancellationToken token = default)
        {
            if (_comenzar == null)
                return;

            await _comenzar(token);
        }

        protected async Task FinalizarAsync(CancellationToken token = default)
        {
            if (_finalizar == null)
                return;

            await _finalizar(token);
        }

        public abstract void Preparar(ICollection<Parametro> parametros, IConfiguracion configuracion, ILogger logger);
        public abstract Task EjecutarAsync(IStream<TRead, TWrite> stream, CancellationToken token = default);
    }
}
