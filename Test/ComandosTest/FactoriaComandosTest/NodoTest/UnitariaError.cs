using Microsoft.Extensions.Logging;
using Moq;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.LineaComandos.Excepcion;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosTest.FactoriaComandosTest.NodoTest
{
    public class UnitariaError
    {
        [Fact]
        public void ErrorRutaInexistente()
        {
            ICollection<string> ruta = new List<string> { "nodo1", "nodo2", "nodo_inexistente", "comando" };
            var nodo = new Nodo<string, string>();
            nodo.Add("nodo1").Add("nodo2").Add("nodo3").Add("comando", new Nodo<string, string>(new ComandoPrueba()));

            Assert.Throws<NoEncontradoExcepcion>(() => nodo.Get("no_encontrado"));

            var configuracionMock = new Mock<IConfiguracion>();
            var loggerMock = new Mock<ILogger>();

            Stack<string> stack = new Stack<string>(ruta.Reverse());

            PER.Comandos.LineaComandos.Excepcion.NoEncontradoExcepcion e = Assert.Throws<PER.Comandos.LineaComandos.Excepcion.NoEncontradoExcepcion>(() => nodo.Crear(stack, new List<Parametro> { new Parametro { Nombre = "--parametro1", Valor = "valor1" } }, configuracionMock.Object, loggerMock.Object));
        }

        [Fact]
        public void ErrorComandoNoDefinido()
        {
            ICollection<string> ruta = new List<string> { "nodo1", "nodo2", "nodo3", "comando" };
            var nodo = new Nodo<string, string>();
            nodo.Add("nodo1").Add("nodo2").Add("nodo3").Add("comando");

            var configuracionMock = new Mock<IConfiguracion>();
            var loggerMock = new Mock<ILogger>();

            Stack<string> stack = new Stack<string>(ruta.Reverse());

            Assert.Throws<NullReferenceException>(() => nodo.Crear(stack, new List<Parametro> { new Parametro { Nombre = "--parametro1", Valor = "valor1" } }, configuracionMock.Object, loggerMock.Object));
        }
    }
}
