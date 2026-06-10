using PER.Mensajeria.Servicio.Contexto;

namespace ServicioTest;

public class ContextoConversacionActivoServicioTest
{
    [Fact]
    public async Task EjecutarAsync_MismaConversacion_DebeSerializar()
    {
        IContextoConversacionActivoServicio servicio = new ContextoConversacionActivoServicio();
        int activos = 0;
        int maximoActivos = 0;

        Func<CancellationToken, Task> accion = async cancellationToken =>
        {
            int activosActuales = Interlocked.Increment(ref activos);
            ActualizarMaximo(ref maximoActivos, activosActuales);

            await Task.Delay(80, cancellationToken);

            Interlocked.Decrement(ref activos);
        };

        Task primeraTarea = servicio.EjecutarAsync(10, accion, CancellationToken.None);
        Task segundaTarea = servicio.EjecutarAsync(10, accion, CancellationToken.None);

        await Task.WhenAll(primeraTarea, segundaTarea).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(1, maximoActivos);
    }

    [Fact]
    public async Task EjecutarAsync_ConversacionesDistintas_DebePermitirParalelo()
    {
        IContextoConversacionActivoServicio servicio = new ContextoConversacionActivoServicio();
        TaskCompletionSource segundoActivo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int activos = 0;
        int maximoActivos = 0;

        Func<CancellationToken, Task> accion = async cancellationToken =>
        {
            int activosActuales = Interlocked.Increment(ref activos);
            ActualizarMaximo(ref maximoActivos, activosActuales);

            if (activosActuales == 2)
            {
                segundoActivo.TrySetResult();
            }

            await Task.Delay(120, cancellationToken);

            Interlocked.Decrement(ref activos);
        };

        Task primeraTarea = servicio.EjecutarAsync(10, accion, CancellationToken.None);
        Task segundaTarea = servicio.EjecutarAsync(20, accion, CancellationToken.None);

        await segundoActivo.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.WhenAll(primeraTarea, segundaTarea).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(2, maximoActivos);
    }

    [Fact]
    public async Task EjecutarAsync_CuandoAccionFalla_DebeLiberarBloqueo()
    {
        IContextoConversacionActivoServicio servicio = new ContextoConversacionActivoServicio();
        bool segundaAccionEjecutada = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EjecutarAsync(10, _ => throw new InvalidOperationException("Fallo controlado."), CancellationToken.None));

        await servicio.EjecutarAsync(10, _ =>
        {
            segundaAccionEjecutada = true;
            return Task.CompletedTask;
        }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(segundaAccionEjecutada);
    }

    private static void ActualizarMaximo(ref int maximoActual, int valor)
    {
        while (true)
        {
            int valorActual = maximoActual;

            if (valor <= valorActual)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref maximoActual, valor, valorActual) == valorActual)
            {
                return;
            }
        }
    }
}
