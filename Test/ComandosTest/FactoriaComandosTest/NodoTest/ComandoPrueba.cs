using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Stream;

namespace ComandosTest.FactoriaComandosTest.NodoTest
{
    internal class ComandoPrueba : ComandoBase<string, string>
    {
        public override void Preparar(ICollection<Parametro> parametros)
        { }

        public override async Task EjecutarAsync(IStream<string, string> stream, CancellationToken token = default)
        {
            await EmpezarAsync(token);

            // Código de comando
            
            await FinalizarAsync(token);
        }
    }
}
