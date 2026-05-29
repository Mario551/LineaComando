using PER.Mensajeria.Core.Utilidad;

namespace CoreTest;

public class UtilidadesMensajeriaTest
{
    [Fact]
    public void Normalizar_DebeQuitarSeparadoresBasicosDelTelefono()
    {
        string telefono = " (300) 123-45-67 ";

        string normalizado = NormalizadorTelefono.Normalizar(telefono);

        Assert.Equal("3001234567", normalizado);
    }

    [Fact]
    public void ObtenerFechaActual_DebeUsarFechaLocalDelSistema()
    {
        RelojSistema relojSistema = new();
        DateTime antes = DateTime.Now.AddSeconds(-1);

        DateTime fechaActual = relojSistema.ObtenerFechaActual();

        DateTime despues = DateTime.Now.AddSeconds(1);
        Assert.InRange(fechaActual, antes, despues);
        Assert.Equal(DateTimeKind.Local, fechaActual.Kind);
    }
}
