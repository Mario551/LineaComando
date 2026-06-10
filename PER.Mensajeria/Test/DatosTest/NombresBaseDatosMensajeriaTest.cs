using PER.Mensajeria.Datos.Esquema;

namespace DatosTest;

public class NombresBaseDatosMensajeriaTest
{
    [Fact]
    public void Postgres_DebeCalificarObjetosConComillasDobles()
    {
        NombresBaseDatosMensajeria nombres = NombresBaseDatosMensajeria.Postgres("mensajeria");

        Assert.Equal("\"mensajeria\".\"per_mensajes\"", nombres.Mensajes);
        Assert.Equal("\"mensajeria\".\"per_procesamientos_internos_mensaje\"", nombres.ProcesamientosInternosMensaje);
    }

    [Fact]
    public void SqlServer_DebeCalificarObjetosConCorchetes()
    {
        NombresBaseDatosMensajeria nombres = NombresBaseDatosMensajeria.SqlServer("mensajeria");

        Assert.Equal("[mensajeria].[per_mensajes]", nombres.Mensajes);
        Assert.Equal("[mensajeria].[per_procesamientos_internos_mensaje]", nombres.ProcesamientosInternosMensaje);
    }

    [Fact]
    public void NormalizarEsquema_DebeRechazarIdentificadorInvalido()
    {
        Assert.Throws<ArgumentException>(() => NombresBaseDatosMensajeria.Postgres("schema-invalido"));
    }
}
