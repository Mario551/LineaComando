using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
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
        IMensajeServicio servicio = new MensajeServicio(
            registrar,
            new FakeRenovarLineaContextoAplicacion(),
            cola);
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

    [Fact]
    public async Task RenovarLineaContextoAsync_DebePublicarEventoConLineaNueva()
    {
        FakeRegistrarMensajeEntranteAplicacion registrar = new();
        FakeRenovarLineaContextoAplicacion renovar = new();
        FakeColaEventosMensajeriaServicio cola = new();
        IMensajeServicio servicio = new MensajeServicio(registrar, renovar, cola);
        SolicitudRenovarLineaContexto solicitud = new()
        {
            IDProcesamientoInternoMensaje = 4,
            IDMensaje = 1,
            IDConversacion = 2,
            IDLineaConversacionOrigen = 3,
            Compactacion = ResultadoCompactacionIntencionContexto.Exito(
                "snapshot",
                new MetadataRazonamientoIAContexto
                {
                    Proveedor = "fake",
                    Modelo = "fake",
                    Adaptador = "fake"
                })
        };

        ResultadoRenovarLineaContexto resultado = await servicio.RenovarLineaContextoAsync(
            solicitud,
            CancellationToken.None);

        Assert.Same(solicitud, renovar.Solicitud);
        Assert.NotNull(cola.EventoPublicado);
        Assert.Equal(resultado.IDMensaje, cola.EventoPublicado.IDMensaje);
        Assert.Equal(resultado.IDProcesamientoInternoMensaje, cola.EventoPublicado.IDProcesamientoInternoMensaje);
        Assert.Equal(resultado.IDConversacion, cola.EventoPublicado.IDConversacion);
        Assert.Equal(resultado.IDLineaConversacion, cola.EventoPublicado.IDLineaConversacion);
    }
}
