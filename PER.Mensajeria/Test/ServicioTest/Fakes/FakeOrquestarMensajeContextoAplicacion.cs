using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;

namespace ServicioTest.Fakes;

public class FakeOrquestarMensajeContextoAplicacion : IOrquestarMensajeContextoAplicacion
{
    public long? IDProcesamientoInternoMensaje { get; private set; }
    public IReadOnlyList<long> IDsProcesamientosInternosMensaje { get; private set; } = [];

    public ResultadoOrquestarMensajeContexto Resultado { get; set; } = ResultadoOrquestarMensajeContexto.Procesado();

    public Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        IDProcesamientoInternoMensaje = idProcesamientoInternoMensaje;
        IDsProcesamientosInternosMensaje = [idProcesamientoInternoMensaje];

        return Task.FromResult(Resultado);
    }

    public Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        CancellationToken cancellationToken)
    {
        IDsProcesamientosInternosMensaje = idsProcesamientosInternosMensaje;
        IDProcesamientoInternoMensaje = idsProcesamientosInternosMensaje.FirstOrDefault();

        return Task.FromResult(Resultado);
    }
}
