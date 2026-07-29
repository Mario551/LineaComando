using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public interface IOpenCodeAgenteAdaptador
{
    DTOOpenCodeMensajeSolicitud CrearSolicitudDecision(
        SolicitudIntencionContexto solicitud);

    ResultadoIntencionContexto InterpretarDecision(
        SolicitudIntencionContexto solicitud,
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado);

    DTOOpenCodeMensajeSolicitud CrearSolicitudCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        IReadOnlyList<string> fragmentos);

    ResultadoCompactacionOpenCode InterpretarCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado);

    InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaError(
        int iteracion,
        string accion,
        string error,
        string? solicitudJson = null,
        string? respuestaJson = null);
}
