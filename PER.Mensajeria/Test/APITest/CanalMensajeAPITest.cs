using PER.Mensajeria.API.Canal;
using PER.Mensajeria.Entidad.DTO;

namespace APITest;

public class CanalMensajeAPITest
{
    [Fact]
    public async Task EnviarAsync_DebeRetornarResultadoEnvio()
    {
        ICanalMensajeAPI canalMensajeAPI = new CanalMensajeAPI();
        DTOMensajeSaliente mensaje = new()
        {
            IDConversacion = 1,
            IDLineaConversacion = 2,
            TipoMensaje = "texto",
            Contenido = "respuesta",
            FechaMensaje = DateTime.Now
        };

        DTOResultadoEnvioMensaje resultado = await canalMensajeAPI.EnviarAsync(mensaje, CancellationToken.None);

        Assert.NotNull(resultado);
    }
}
