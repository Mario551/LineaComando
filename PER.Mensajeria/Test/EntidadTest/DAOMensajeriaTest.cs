using PER.Mensajeria.Entidad.DAO;

namespace EntidadTest;

public class DAOMensajeriaTest
{
    [Fact]
    public void DAOMensaje_DebeRelacionarseConLineaConversacionYNoConConversacionDirecta()
    {
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = 10,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            IdentificadorExternoMensaje = "externo-1"
        };

        Assert.Equal(10, mensaje.IDLineaConversacion);
        Assert.Equal("texto", mensaje.IDTipoMensaje);
        Assert.Equal("entrada", mensaje.IDDireccionMensaje);
        Assert.Null(typeof(DAOMensaje).GetProperty("IDConversacion"));
    }

    [Fact]
    public void DAOArchivoMensaje_DebeGuardarUbicacionYNoContenidoBinario()
    {
        DAOArchivoMensaje archivoMensaje = new()
        {
            IDMensaje = 1,
            IDTipoContenidoArchivo = "image/png",
            UbicacionArchivo = "s3://bucket/archivo.png",
            ProveedorAlmacenamiento = "s3"
        };

        Assert.Equal("s3://bucket/archivo.png", archivoMensaje.UbicacionArchivo);
        Assert.Equal("s3", archivoMensaje.ProveedorAlmacenamiento);
        Assert.Null(typeof(DAOArchivoMensaje).GetProperty("Contenido"));
        Assert.Null(typeof(DAOArchivoMensaje).GetProperty("Binario"));
    }

    [Fact]
    public void DAOProcesamientoInternoMensaje_DebeRepresentarTrabajoDelOrquestador()
    {
        DAOProcesamientoInternoMensaje procesamiento = new()
        {
            IDMensaje = 1,
            IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
            IDEstadoProcesamientoInternoMensaje = "pendiente",
            Intentos = 0
        };

        Assert.Equal("orquestar_entrada", procesamiento.IDTipoProcesamientoInternoMensaje);
        Assert.Equal("pendiente", procesamiento.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(0, procesamiento.Intentos);
        Assert.Null(procesamiento.FechaProcesado);
    }

    [Fact]
    public void DAOEnvioMensaje_DebeRepresentarEnvioExternoDeMensajeSaliente()
    {
        DAOEnvioMensaje envio = new()
        {
            IDMensaje = 20,
            IDEstadoEnvioMensaje = "pendiente",
            Intentos = 0
        };

        Assert.Equal(20, envio.IDMensaje);
        Assert.Equal("pendiente", envio.IDEstadoEnvioMensaje);
        Assert.Null(envio.FechaEnviado);
    }
}
