using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.Mapeo;

public sealed class ResultadoConversionMensajeEntranteInfobip
{
    private ResultadoConversionMensajeEntranteInfobip()
    {
    }

    public bool Convertido { get; private init; }
    public DTORegistrarMensajeEntranteSolicitud? Solicitud { get; private init; }
    public string? Error { get; private init; }

    public static ResultadoConversionMensajeEntranteInfobip Exito(
        DTORegistrarMensajeEntranteSolicitud solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return new ResultadoConversionMensajeEntranteInfobip
        {
            Convertido = true,
            Solicitud = solicitud
        };
    }

    public static ResultadoConversionMensajeEntranteInfobip Fallo(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("El error de conversion es obligatorio.", nameof(error));
        }

        return new ResultadoConversionMensajeEntranteInfobip
        {
            Error = error
        };
    }
}
