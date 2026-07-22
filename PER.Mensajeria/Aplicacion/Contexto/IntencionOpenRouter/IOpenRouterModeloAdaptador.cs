using PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public interface IOpenRouterModeloAdaptador
{
    int MaximoLlamadasCompactacion { get; }

    DTOOpenRouterSolicitudChat CrearSolicitudDecision(SolicitudIntencionContexto solicitud);

    ResultadoIntencionContexto InterpretarDecision(
        SolicitudIntencionContexto solicitud,
        ResultadoOpenRouterCliente resultado);

    DTOOpenRouterSolicitudChat CrearSolicitudCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        IReadOnlyList<string> fragmentos);

    ResultadoCompactacionOpenRouter InterpretarCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        ResultadoOpenRouterCliente resultado);

    InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaError(
        int iteracion,
        string accion,
        string error);
}
