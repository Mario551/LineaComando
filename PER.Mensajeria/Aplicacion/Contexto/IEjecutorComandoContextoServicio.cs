namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IEjecutorComandoContextoServicio
{
    Task<ResultadoComandoContexto> EjecutarAsync(
        SolicitudEjecutarComandoContexto solicitud,
        CancellationToken cancellationToken);
}
