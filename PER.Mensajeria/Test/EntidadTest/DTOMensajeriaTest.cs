using PER.Mensajeria.Entidad.DTO;

namespace EntidadTest;

public class DTOMensajeriaTest
{
    [Fact]
    public void DTOMensajeEntrante_DebeInicializarColeccionDeArchivos()
    {
        DTOMensajeEntrante mensaje = new();

        Assert.NotNull(mensaje.Archivos);
        Assert.Empty(mensaje.Archivos);
    }

    [Fact]
    public void DTOMensajeSaliente_DebeInicializarColeccionDeArchivos()
    {
        DTOMensajeSaliente mensaje = new();

        Assert.NotNull(mensaje.Archivos);
        Assert.Empty(mensaje.Archivos);
    }

    [Fact]
    public void DTOArchivoMensaje_DebeGuardarReferenciaDeRecuperacion()
    {
        DTOArchivoMensaje archivo = new()
        {
            TipoContenido = "application/pdf",
            UbicacionArchivo = "minio://mensajes/documento.pdf",
            ProveedorAlmacenamiento = "minio"
        };

        Assert.Equal("application/pdf", archivo.TipoContenido);
        Assert.Equal("minio://mensajes/documento.pdf", archivo.UbicacionArchivo);
        Assert.Equal("minio", archivo.ProveedorAlmacenamiento);
    }

    [Fact]
    public void DTOEnvioMensajePendiente_DebeInicializarMensajeSaliente()
    {
        DTOEnvioMensajePendiente envio = new();

        Assert.NotNull(envio.Mensaje);
        Assert.NotNull(envio.Mensaje.Archivos);
    }
}
