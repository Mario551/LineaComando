using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

namespace ServicioTest.Fakes;

public class FakeOrquestarMensajeEntradaAplicacion : IOrquestarMensajeEntradaAplicacion
{
    public long? IDProcesamientoInternoMensaje { get; private set; }

    public Task EjecutarAsync(long idProcesamientoInternoMensaje, CancellationToken cancellationToken)
    {
        IDProcesamientoInternoMensaje = idProcesamientoInternoMensaje;

        return Task.CompletedTask;
    }
}
