using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace ComandosColaTest
{
    public class NombresBaseDatosTest
    {
        [Fact]
        public void Postgres_DebeCalificarConEsquema()
        {
            NombresBaseDatos nombres = NombresBaseDatos.Postgres("linea_comando");

            Assert.Equal("\"linea_comando\".\"per_cola_comandos\"", nombres.ColaComandos);
            Assert.Equal("\"linea_comando\".\"per_cola_comandos_resultados\"", nombres.ColaComandosResultados);
            Assert.Equal("\"linea_comando\".\"obtener_comandos_pendientes\"", nombres.ObtenerComandosPendientes);
        }

        [Fact]
        public void SqlServer_DebeCalificarConEsquema()
        {
            NombresBaseDatos nombres = NombresBaseDatos.SqlServer("linea_comando");

            Assert.Equal("[linea_comando].[per_cola_comandos]", nombres.ColaComandos);
            Assert.Equal("[linea_comando].[per_cola_comandos_resultados]", nombres.ColaComandosResultados);
            Assert.Equal("[linea_comando].[obtener_comandos_pendientes]", nombres.ObtenerComandosPendientes);
        }

        [Fact]
        public void EsquemaInvalido_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentException>(() => NombresBaseDatos.Postgres("public;drop"));
        }
    }
}
