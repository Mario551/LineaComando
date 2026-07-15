using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

namespace ServicioTest.Fakes;

public class FakeOrquestarMensajeEntradaAplicacion : IOrquestarMensajeEntradaAplicacion
{
    public long? IDProcesamientoInternoMensaje { get; private set; }

    public ResultadoOrquestarMensajeEntrada Resultado { get; set; } = ResultadoOrquestarMensajeEntrada.Procesado();

    public Task<ResultadoOrquestarMensajeEntrada> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        IDProcesamientoInternoMensaje = idProcesamientoInternoMensaje;

        return Task.FromResult(Resultado);
    }
}
