using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Comando;

namespace ComandosTest.FactoriaComandosTest.NodoTest
{
    internal class ComandoPrueba : ComandoBase<string, string>
    {
        public override void Preparar(ICollection<Parametro> parametros)
        { }

        public override async Task<string> EjecutarAsync(string entrada, CancellationToken token = default)
        {
            await EmpezarAsync(token);
            
            await FinalizarAsync(token);

            return entrada;
        }
    }
}
