using PER.Comandos.LineaComandos;
using PER.Comandos.LineaComandos.Atributo;

namespace ComandosTest.LineaComandosTest
{
    public class UnitariaOk
    {
        [Fact]
        public void Ok()
        {
            var ruta = new List<string> { "nodo1", "nodo2", "nodo3", "comando", "--parametro1=valor1", "--parametro2" };
            var nodos = new List<string> { "nodo1", "nodo2", "nodo3" };
            var parametros = new List<Parametro> 
            {
                new Parametro { Nombre = "--parametro1", Valor = "valor1" },
                new Parametro { Nombre = "--parametro2" }
            };

            LineaComando lineaComando = new LineaComando(ruta);

            Assert.NotEmpty(lineaComando.Ruta);
            Assert.NotEmpty(lineaComando.Parametros);

            for (int i = 0; i < nodos.Count; i++)
                Assert.Equal(nodos[i], lineaComando.Ruta.ElementAt(i));

            Assert.Equal(2, lineaComando.Parametros.Count);
            for (int i = 0; i < parametros.Count; i++)
            {
                Assert.Equal(parametros[i].Nombre, lineaComando.Parametros.ElementAt(i).Nombre);
                Assert.Equal(parametros[i].Valor, lineaComando.Parametros.ElementAt(i).Valor);
            }

            ruta = new List<string> { "comando1" };

            lineaComando = new LineaComando(ruta);

            Assert.Single(lineaComando.Ruta);
            Assert.Empty(lineaComando.Parametros);
            Assert.Equal("comando1", lineaComando.Ruta.ElementAt(0));
        }
    }
}
