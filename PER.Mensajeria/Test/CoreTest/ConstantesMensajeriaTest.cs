using PER.Mensajeria.Core.Constante;

namespace CoreTest;

public class ConstantesMensajeriaTest
{
    [Fact]
    public void CatalogosBase_DebenCoincidirConModeloMensajeria()
    {
        Assert.Equal("entrada", DireccionMensajeConstante.Entrada);
        Assert.Equal("salida", DireccionMensajeConstante.Salida);
        Assert.Equal("orquestar_entrada", TipoProcesamientoInternoMensajeConstante.OrquestarEntrada);
        Assert.Equal("pendiente", EstadoProcesamientoInternoMensajeConstante.Pendiente);
        Assert.Equal("en_proceso", EstadoProcesamientoInternoMensajeConstante.EnProceso);
        Assert.Equal("procesado", EstadoProcesamientoInternoMensajeConstante.Procesado);
        Assert.Equal("error", EstadoProcesamientoInternoMensajeConstante.Error);
        Assert.Equal("pendiente", EstadoEnvioMensajeConstante.Pendiente);
        Assert.Equal("enviado", EstadoEnvioMensajeConstante.Enviado);
        Assert.Equal("fallido", EstadoEnvioMensajeConstante.Fallido);
    }

    [Fact]
    public void TiposMensaje_DebenCoincidirConCatalogoBase()
    {
        Assert.Equal("texto", TipoMensajeConstante.Texto);
        Assert.Equal("imagen", TipoMensajeConstante.Imagen);
        Assert.Equal("audio", TipoMensajeConstante.Audio);
        Assert.Equal("video", TipoMensajeConstante.Video);
        Assert.Equal("documento", TipoMensajeConstante.Documento);
        Assert.Equal("ubicacion", TipoMensajeConstante.Ubicacion);
    }
}
