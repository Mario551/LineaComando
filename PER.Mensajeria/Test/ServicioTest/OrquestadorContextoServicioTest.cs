using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Orquestador;
using ServicioTest.Fakes;

namespace ServicioTest;

public class OrquestadorContextoServicioTest
{
    [Fact]
    public async Task ProcesarAsync_EventoEntrada_DebeEjecutarPasosFuncionalesDelOrquestador()
    {
        FakeOrquestarMensajeEntradaAplicacion aplicacion = new();
        IOrquestadorContextoServicio servicio = new OrquestadorContextoServicio(aplicacion);
        EventoMensajeria evento = CrearEvento();

        await servicio.ProcesarAsync(evento, CancellationToken.None);

        Assert.Equal(evento.IDProcesamientoInternoMensaje, aplicacion.IDProcesamientoInternoMensaje);
    }

    [Fact]
    public async Task ProcesarAsync_ErrorContexto_DebeMarcarProcesamientoComoError()
    {
        FakeOrquestarMensajeEntradaAplicacion aplicacion = new();
        IOrquestadorContextoServicio servicio = new OrquestadorContextoServicio(aplicacion);
        EventoMensajeria evento = CrearEvento();

        await servicio.ProcesarAsync(evento, CancellationToken.None);

        Assert.Equal(evento.IDProcesamientoInternoMensaje, aplicacion.IDProcesamientoInternoMensaje);
    }

    private static EventoMensajeria CrearEvento()
    {
        return new EventoMensajeria
        {
            IDMensaje = 1,
            IDProcesamientoInternoMensaje = 2,
            IDConversacion = 3,
            IDLineaConversacion = 4,
            FechaCreacion = DateTime.Now
        };
    }
}
