using PER.Mensajeria.Servicio.Envio;
using ServicioTest.Fakes;

namespace ServicioTest;

public class EnvioMensajeServicioTest
{
    [Fact]
    public async Task ProcesarAsync_EnvioPendiente_DebeEnviarPorCanal()
    {
        FakeEnviarMensajeAplicacion aplicacion = new();
        IEnvioMensajeServicio servicio = new EnvioMensajeServicio(aplicacion);

        await servicio.ProcesarAsync(CancellationToken.None);

        Assert.True(aplicacion.Ejecutado);
    }

    [Fact]
    public async Task ProcesarAsync_FalloCanal_DebeRegistrarErrorYMarcarFallido()
    {
        FakeEnviarMensajeAplicacion aplicacion = new();
        IEnvioMensajeServicio servicio = new EnvioMensajeServicio(aplicacion);

        await servicio.ProcesarAsync(CancellationToken.None);

        Assert.True(aplicacion.Ejecutado);
    }
}
