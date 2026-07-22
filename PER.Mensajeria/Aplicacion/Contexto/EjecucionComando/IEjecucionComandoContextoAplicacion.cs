using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

public interface IEjecucionComandoContextoAplicacion
{
    string Proveedor { get; }

    Task<ResultadoEjecucionComandoContexto?> ReanudarActivaAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        CancellationToken cancellationToken);

    Task<ResultadoEjecucionComandoContexto> EjecutarAsync(
        SolicitudContextoConversacion solicitud,
        EjecucionComandoContexto ejecucion,
        ComandoContexto comando,
        IReadOnlyDictionary<string, string> parametros,
        CancellationToken cancellationToken);
}
