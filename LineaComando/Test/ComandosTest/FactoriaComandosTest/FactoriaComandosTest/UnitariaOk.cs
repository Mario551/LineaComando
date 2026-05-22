using PER.Comandos.LineaComandos;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosTest.FactoriaComandosTest.FactoriaComandosTest
{
    public class UnitariaOk
    {
        [Fact]
        public void Ok()
        {
            var rutaComando1 = new List<string> { "nodo1", "nodo2", "nodo3", "comando1", "--parametro1"};
            var rutaComando2 = new List<string> { "nodo1", "nodo2", "nodo3", "comando2", "--parametro1"};
            var rutaComando3 = new List<string> { "comando3" };

            var factoria = new FactoriaComandos<string, string>();
            factoria.Add("nodo1").Add("nodo2").Add("nodo3")
                .Add("comando1", new Nodo<string, string>(new ComandoPrueba1()))
                .Padre?.Add("comando2", new Nodo<string, string>(new ComandoPrueba2()));

            factoria.Add("comando3", new Nodo<string, string>(new ComandoPrueba2()));

            ComprobarRutaYTipoComando<ComandoPrueba1>(rutaComando1, factoria);
            ComprobarRutaYTipoComando<ComandoPrueba2>(rutaComando2, factoria); 
            ComprobarRutaYTipoComando<ComandoPrueba2>(rutaComando3, factoria);
        }

        private void ComprobarRutaYTipoComando<T>(ICollection<string> ruta, FactoriaComandos<string, string> factoria)
        {
            LineaComando lineaComando = new LineaComando(ruta);

            IComando<string, string>? comando = null;
            try
            {
                comando = factoria.Crear(lineaComando);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }

            Assert.IsType<T>(comando);
        }
    }
}
