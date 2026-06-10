using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Mensaje;
using ServicioTest.Fakes;

namespace ServicioTest;

public class MensajeServicioTest
{
    [Fact]
    public async Task RecibirAsync_MensajeEntrante_DebeRegistrarYPublicarEvento()
    {
        FakeRegistrarMensajeEntranteAplicacion registrar = new();
        FakeColaEventosMensajeriaServicio cola = new();
        IMensajeServicio servicio = new MensajeServicio(registrar, cola);
        DTORegistrarMensajeEntranteSolicitud solicitud = new()
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = "whatsapp",
                Cuenta = "cuenta-prueba",
                IdentificadorParticipante = "3001234567",
                TipoParticipante = "telefono",
                TipoMensaje = "texto",
                IdentificadorExternoMensaje = "externo-servicio-1",
                FechaMensaje = DateTime.Now
            }
        };

        DTORegistrarMensajeEntranteRespuesta respuesta = await servicio.RecibirAsync(solicitud, CancellationToken.None);

        Assert.True(registrar.Ejecutado);
        Assert.Same(solicitud, registrar.Solicitud);
        Assert.True(respuesta.Registrado);
        Assert.NotNull(cola.EventoPublicado);
        Assert.Equal(respuesta.IDMensaje, cola.EventoPublicado.IDMensaje);
        Assert.Equal(respuesta.IDConversacion, cola.EventoPublicado.IDConversacion);
        Assert.Equal(respuesta.IDLineaConversacion, cola.EventoPublicado.IDLineaConversacion);
        Assert.Equal(respuesta.IDProcesamientoInternoMensaje, cola.EventoPublicado.IDProcesamientoInternoMensaje);
    }
}
