namespace PER.Mensajeria.Aplicacion.RenovarLineaContexto;

public interface IRenovarLineaContextoAplicacion
{
    Task<ResultadoRenovarLineaContexto> EjecutarAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken);
}
