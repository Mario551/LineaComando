namespace PER.Mensajeria.API.Contexto;

public interface IEjecutorComandoContextoServicio
{
    Task<DTOResultadoComandoContexto> EjecutarAsync(
        DTOEjecutarComandoContextoSolicitud solicitud,
        CancellationToken cancellationToken);
}
