using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;

namespace ServicioTest.Fakes;

public class FakeOrquestarMensajeContextoAplicacion : IOrquestarMensajeContextoAplicacion
{
    public long? IDProcesamientoInternoMensaje { get; private set; }

    public ResultadoOrquestarMensajeContexto Resultado { get; set; } = ResultadoOrquestarMensajeContexto.Procesado();

    public Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        IDProcesamientoInternoMensaje = idProcesamientoInternoMensaje;

        return Task.FromResult(Resultado);
    }
}
