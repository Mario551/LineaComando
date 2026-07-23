using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;

namespace AplicacionTest;

public class ColaEventosMensajeriaSalidaServicioTest
{
    [Fact]
    public async Task PublicarYConsumirAsync_DebeRetornarEventoPublicado()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        EventoMensajeriaSalida evento = CrearEvento(1);

        cola.Publicar(evento);

        EventoMensajeriaSalida consumido = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(evento, consumido);
    }

    [Fact]
    public async Task ConsumirAsync_DebeRespetarOrdenFifo()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        cola.Publicar(CrearEvento(1));
        cola.Publicar(CrearEvento(2));
        cola.Publicar(CrearEvento(3));

        EventoMensajeriaSalida primero = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaSalida segundo = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaSalida tercero = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Equal(1, primero.IDEnvioMensaje);
        Assert.Equal(2, segundo.IDEnvioMensaje);
        Assert.Equal(3, tercero.IDEnvioMensaje);
    }

    [Fact]
    public async Task PublicarRehidratado_DebeAnteponerloALaColaViva()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        EventoMensajeriaSalida eventoVivo = CrearEvento(2);
        EventoMensajeriaSalida eventoRehidratado = CrearEvento(1);
        cola.Publicar(eventoVivo);
        cola.PublicarRehidratado(eventoRehidratado);

        EventoMensajeriaSalida primero = await cola.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaSalida segundo = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(eventoRehidratado, primero);
        Assert.Same(eventoVivo, segundo);
    }

    [Fact]
    public async Task Publicar_MismoEnvioDosVeces_DebeEvitarDuplicado()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        cola.Publicar(CrearEvento(1));
        cola.Publicar(CrearEvento(1));

        EventoMensajeriaSalida evento = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Equal(1, evento.IDEnvioMensaje);
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task PublicarRehidratado_EnvioVivoExistente_DebeConservarUnaSolaCopia()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        EventoMensajeriaSalida vivo = CrearEvento(1);
        EventoMensajeriaSalida rehidratado = CrearEvento(1);
        cola.Publicar(vivo);
        cola.PublicarRehidratado(rehidratado);

        EventoMensajeriaSalida evento = await cola.ConsumirAsync(CancellationToken.None);

        Assert.Same(vivo, evento);
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ConsumirAsync_SinEventos_DebeEsperarHastaPublicar()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));
        Task<EventoMensajeriaSalida> consumo = cola.ConsumirAsync(cancellationTokenSource.Token);

        Assert.False(consumo.IsCompleted);

        EventoMensajeriaSalida evento = CrearEvento(10);
        cola.Publicar(evento);

        Assert.Same(evento, await consumo);
    }

    [Fact]
    public async Task ConsumirAsync_Cancelado_DebeLanzarOperationCanceledException()
    {
        ColaEventosMensajeriaSalidaServicio cola = new();
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cola.ConsumirAsync(cancellationTokenSource.Token));
    }

    private static EventoMensajeriaSalida CrearEvento(long idEnvioMensaje)
    {
        return new EventoMensajeriaSalida
        {
            IDEnvioMensaje = idEnvioMensaje,
            FechaCreacion = DateTime.Now
        };
    }
}
