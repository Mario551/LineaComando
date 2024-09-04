using Microsoft.Extensions.Logging;
using Moq;
using PER.Comandos.LineaComandos;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.LineaComandos.Excepcion;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosTest.FactoriaComandosTest.FactoriaComandosTest
{
    public class UnitariaError
    {
        [Fact]
        public void ErrorRutaInexistente()
        {
            var rutaComando1 = new List<string> { "nodo1", "nodo2", "nodo3", "comando1", "--parametro1"};
            var rutaComando2 = new List<string> { "nodo1", "nodo2", "nodo3", "comando2", "--parametro1"};
            var rutaInexistente = new List<string> { "nodo1", "nodo2", "nodo_inexistente", "comando1", "--parametro1"};

            var factoria = new FactoriaComandos<string, string>();
            factoria.Add("nodo1").Add("nodo2").Add("nodo3")
                .Add("comando1", new Nodo<string, string>(new ComandoPrueba1()))
                .Padre?.Add("comando2", new Nodo<string, string>(new ComandoPrueba2()));

            LineaComando lineaComando = new LineaComando(rutaInexistente);
            var configuracionMock = new Mock<IConfiguracion>();
            var loggerMock = new Mock<ILogger>();

            Assert.Throws<NoEncontradoExcepcion>(() => factoria.Crear(lineaComando, configuracionMock.Object, loggerMock.Object));
        }
    }
}
