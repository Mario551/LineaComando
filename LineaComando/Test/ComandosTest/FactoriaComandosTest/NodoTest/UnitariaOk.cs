using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosTest.FactoriaComandosTest.NodoTest
{
    public class UnitariaOk
    {
        [Fact]
        public void AddOk()
        {
            ICollection<string> ruta = new List<string> { "nodo1", "nodo2", "nodo3", "comando" };
            var nodo = new Nodo<string, string>();
            nodo.Add("nodo1").Add("nodo2").Add("nodo3").Add("comando", new Nodo<string, string>(new ComandoPrueba()));

            Nodo<string, string> hijo = nodo.Get("nodo1");
            Assert.Equal("nodo1", hijo.Nombre);

            hijo = hijo.Get("nodo2");
            Assert.Equal("nodo2", hijo.Nombre);

            hijo = hijo.Get("nodo3");
            Assert.Equal("nodo3", hijo.Nombre);

            Stack<string> stack = new Stack<string>(ruta.Reverse());

            IComando<string, string>? comando = null;
            try
            {
                comando = nodo.Crear(stack, new List<Parametro> { new Parametro { Nombre = "--parametro1", Valor = "valor1" } });
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }

            Assert.NotNull(comando);
        }

        [Fact]
        public void ComandoSinModulosOk()
        {
            ICollection<string> ruta = new List<string> { "comando" };
            var nodo = new Nodo<string, string>();
            nodo.Add("comando", new Nodo<string, string>(new ComandoPrueba()));

            Stack<string> stack = new Stack<string>(ruta.Reverse());

            IComando<string, string>? comando = null;
            try
            {
                comando = nodo.Crear(stack, new List<Parametro> { new Parametro { Nombre = "--parametro1", Valor = "valor1" } });
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }

            Assert.NotNull(comando);
        }
    }
}
