namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IEjecutorComandoContextoServicio
{
    string Proveedor { get; }

    Task<ReferenciaEjecucionComandoContexto> EncolarAsync(
        SolicitudEjecutarComandoContexto solicitud,
        CancellationToken cancellationToken);

    Task<ConsultaEjecucionComandoContexto> ConsultarAsync(
        ReferenciaEjecucionComandoContexto referencia,
        CancellationToken cancellationToken);

    Task<ResultadoComandoContexto> EsperarResultadoAsync(
        ReferenciaEjecucionComandoContexto referencia,
        CancellationToken cancellationToken);

    Task AbandonarAsync(
        ReferenciaEjecucionComandoContexto referencia,
        string motivo,
        CancellationToken cancellationToken);
}
