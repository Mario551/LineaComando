using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

namespace AplicacionTest;

public class ColaEventosMensajeriaEntradaServicioTest
{
    [Fact]
    public async Task PublicarYConsumirAsync_DebeRetornarEventoPublicado()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        EventoMensajeriaEntrada evento = CrearEvento(1);

        cola.Publicar(evento);

        EventoMensajeriaEntrada consumido = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(evento, consumido);
        Assert.Equal(1, consumido.IDMensaje);
    }

    [Fact]
    public async Task ConsumirAsync_DebeRespetarOrdenFifo()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        EventoMensajeriaEntrada primero = CrearEvento(1);
        EventoMensajeriaEntrada segundo = CrearEvento(2);
        EventoMensajeriaEntrada tercero = CrearEvento(3);

        cola.Publicar(primero);
        cola.Publicar(segundo);
        cola.Publicar(tercero);

        EventoMensajeriaEntrada eventoPrimero = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaEntrada eventoSegundo = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaEntrada eventoTercero = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Equal(1, eventoPrimero.IDMensaje);
        Assert.Equal(2, eventoSegundo.IDMensaje);
        Assert.Equal(3, eventoTercero.IDMensaje);
    }

    [Fact]
    public async Task PublicarRehidratado_DebeAnteponerloALaColaViva()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        EventoMensajeriaEntrada eventoVivo = CrearEvento(2);
        EventoMensajeriaEntrada eventoRehidratado = CrearEvento(1);

        cola.Publicar(eventoVivo);
        cola.PublicarRehidratado(eventoRehidratado);

        EventoMensajeriaEntrada primero = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaEntrada segundo = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(eventoRehidratado, primero);
        Assert.Same(eventoVivo, segundo);
    }

    [Fact]
    public async Task PublicarRehidratado_MismoProcesamientoYaPublicado_DebeConservarUnaSolaCopia()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        EventoMensajeriaEntrada eventoVivo = CrearEvento(1);
        EventoMensajeriaEntrada eventoRehidratado = CrearEvento(2);
        eventoRehidratado.IDProcesamientoInternoMensaje = eventoVivo.IDProcesamientoInternoMensaje;

        cola.Publicar(eventoVivo);
        cola.PublicarRehidratado(eventoRehidratado);

        EventoMensajeriaEntrada consumido = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(eventoVivo, consumido);
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task Publicar_MismoProcesamientoDosVeces_DebeEvitarDuplicadoEnCola()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        EventoMensajeriaEntrada primero = CrearEvento(1);
        EventoMensajeriaEntrada duplicado = CrearEvento(2);
        duplicado.IDProcesamientoInternoMensaje = primero.IDProcesamientoInternoMensaje;

        cola.Publicar(primero);
        cola.Publicar(duplicado);

        EventoMensajeriaEntrada consumido = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Equal(primero.IDMensaje, consumido.IDMensaje);
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ConsumirAsync_SinEventos_DebeEsperarHastaPublicar()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));

        Task<EventoMensajeriaEntrada> tareaConsumo = cola.ConsumirAsync(cancellationTokenSource.Token);

        Assert.False(tareaConsumo.IsCompleted);

        EventoMensajeriaEntrada evento = CrearEvento(10);
        cola.Publicar(evento);

        EventoMensajeriaEntrada consumido = await tareaConsumo;

        Assert.Same(evento, consumido);
    }

    [Fact]
    public async Task ConsumirAsync_Cancelado_DebeLanzarOperationCanceledException()
    {
        ColaEventosMensajeriaEntradaServicio cola = new();
        using CancellationTokenSource cancellationTokenSource = new();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    private static EventoMensajeriaEntrada CrearEvento(long idMensaje)
    {
        return new EventoMensajeriaEntrada
        {
            IDMensaje = idMensaje,
            IDProcesamientoInternoMensaje = idMensaje + 100,
            IDConversacion = idMensaje + 200,
            IDLineaConversacion = idMensaje + 300,
            FechaCreacion = DateTime.Now
        };
    }
}
