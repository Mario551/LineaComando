using PER.Mensajeria.Servicio.Cola;

namespace ServicioTest;

public class ColaEventosMensajeriaServicioTest
{
    [Fact]
    public async Task PublicarYConsumirAsync_DebeRetornarEventoPublicado()
    {
        ColaEventosMensajeriaServicio cola = new();
        EventoMensajeria evento = CrearEvento(1);

        cola.Publicar(evento);

        EventoMensajeria consumido = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(evento, consumido);
        Assert.Equal(1, consumido.IDMensaje);
    }

    [Fact]
    public async Task ConsumirAsync_DebeRespetarOrdenFifo()
    {
        ColaEventosMensajeriaServicio cola = new();
        EventoMensajeria primero = CrearEvento(1);
        EventoMensajeria segundo = CrearEvento(2);
        EventoMensajeria tercero = CrearEvento(3);

        cola.Publicar(primero);
        cola.Publicar(segundo);
        cola.Publicar(tercero);

        EventoMensajeria eventoPrimero = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeria eventoSegundo = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeria eventoTercero = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Equal(1, eventoPrimero.IDMensaje);
        Assert.Equal(2, eventoSegundo.IDMensaje);
        Assert.Equal(3, eventoTercero.IDMensaje);
    }

    [Fact]
    public async Task Publicar_MismoProcesamientoDosVeces_DebeEvitarDuplicadoEnCola()
    {
        ColaEventosMensajeriaServicio cola = new();
        EventoMensajeria primero = CrearEvento(1);
        EventoMensajeria duplicado = CrearEvento(2);
        duplicado.IDProcesamientoInternoMensaje = primero.IDProcesamientoInternoMensaje;

        cola.Publicar(primero);
        cola.Publicar(duplicado);

        EventoMensajeria consumido = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Equal(primero.IDMensaje, consumido.IDMensaje);
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ConsumirAsync_SinEventos_DebeEsperarHastaPublicar()
    {
        ColaEventosMensajeriaServicio cola = new();
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));

        Task<EventoMensajeria> tareaConsumo = cola.ConsumirAsync(cancellationTokenSource.Token);

        Assert.False(tareaConsumo.IsCompleted);

        EventoMensajeria evento = CrearEvento(10);
        cola.Publicar(evento);

        EventoMensajeria consumido = await tareaConsumo;

        Assert.Same(evento, consumido);
    }

    [Fact]
    public async Task ConsumirAsync_Cancelado_DebeLanzarOperationCanceledException()
    {
        ColaEventosMensajeriaServicio cola = new();
        using CancellationTokenSource cancellationTokenSource = new();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    private static EventoMensajeria CrearEvento(long idMensaje)
    {
        return new EventoMensajeria
        {
            IDMensaje = idMensaje,
            IDProcesamientoInternoMensaje = idMensaje + 100,
            IDConversacion = idMensaje + 200,
            IDLineaConversacion = idMensaje + 300,
            FechaCreacion = DateTime.Now
        };
    }
}
