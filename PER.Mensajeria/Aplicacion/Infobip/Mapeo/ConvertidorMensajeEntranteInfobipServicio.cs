using System.Text.Json;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Aplicacion.Infobip.Mapeo;

public class ConvertidorMensajeEntranteInfobipServicio :
    IConvertidorMensajeEntranteInfobipServicio
{
    private static readonly IReadOnlyDictionary<(string Tipo, string Extension), string> TiposContenido =
        new Dictionary<(string Tipo, string Extension), string>
        {
            [("IMAGE", ".jpg")] = "image/jpeg",
            [("IMAGE", ".jpeg")] = "image/jpeg",
            [("IMAGE", ".png")] = "image/png",
            [("IMAGE", ".gif")] = "image/gif",
            [("IMAGE", ".webp")] = "image/webp",
            [("DOCUMENT", ".pdf")] = "application/pdf",
            [("DOCUMENT", ".txt")] = "text/plain",
            [("DOCUMENT", ".csv")] = "text/csv",
            [("DOCUMENT", ".doc")] = "application/msword",
            [("DOCUMENT", ".docx")] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [("DOCUMENT", ".xls")] = "application/vnd.ms-excel",
            [("DOCUMENT", ".xlsx")] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [("DOCUMENT", ".ppt")] = "application/vnd.ms-powerpoint",
            [("DOCUMENT", ".pptx")] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [("AUDIO", ".ogg")] = "audio/ogg",
            [("AUDIO", ".mp3")] = "audio/mpeg",
            [("AUDIO", ".aac")] = "audio/aac",
            [("AUDIO", ".amr")] = "audio/amr",
            [("AUDIO", ".opus")] = "audio/opus",
            [("AUDIO", ".wav")] = "audio/wav",
            [("AUDIO", ".m4a")] = "audio/mp4",
            [("VOICE", ".ogg")] = "audio/ogg",
            [("VOICE", ".mp3")] = "audio/mpeg",
            [("VOICE", ".aac")] = "audio/aac",
            [("VOICE", ".amr")] = "audio/amr",
            [("VOICE", ".opus")] = "audio/opus",
            [("VOICE", ".wav")] = "audio/wav",
            [("VOICE", ".m4a")] = "audio/mp4",
            [("VIDEO", ".mp4")] = "video/mp4",
            [("VIDEO", ".3gp")] = "video/3gpp",
            [("VIDEO", ".mov")] = "video/quicktime"
        };

    public ResultadoConversionMensajeEntranteInfobip Convertir(
        WebhookReceiptInfobip recepcion)
    {
        ArgumentNullException.ThrowIfNull(recepcion);
        InboundMessageInfobip mensaje = recepcion.InboundMessageInfobip;
        string tipoMensaje;
        string? contenido;
        List<DTOArchivoMensaje> archivos = [];

        switch (mensaje.Type)
        {
            case "TEXT":
                tipoMensaje = "texto";
                contenido = mensaje.TextMessageInfobip?.Text;
                break;
            case "LOCATION":
                if (mensaje.LocationMessageInfobip is null)
                {
                    return ResultadoConversionMensajeEntranteInfobip.Fallo(
                        "La recepcion LOCATION no contiene su detalle persistido.");
                }

                tipoMensaje = "ubicacion";
                contenido = JsonSerializer.Serialize(new
                {
                    mensaje.LocationMessageInfobip.Latitude,
                    mensaje.LocationMessageInfobip.Longitude,
                    mensaje.LocationMessageInfobip.Address,
                    mensaje.LocationMessageInfobip.Name,
                    mensaje.LocationMessageInfobip.Url
                });
                break;
            case "IMAGE":
                tipoMensaje = "imagen";
                contenido = mensaje.ImageMessageInfobip?.Caption;
                return ConvertirMedio(
                    recepcion,
                    tipoMensaje,
                    contenido,
                    mensaje.Type,
                    mensaje.ImageMessageInfobip?.Url);
            case "DOCUMENT":
                tipoMensaje = "documento";
                contenido = mensaje.DocumentMessageInfobip?.Caption;
                return ConvertirMedio(
                    recepcion,
                    tipoMensaje,
                    contenido,
                    mensaje.Type,
                    mensaje.DocumentMessageInfobip?.Url);
            case "AUDIO":
                tipoMensaje = "audio";
                contenido = mensaje.AudioMessageInfobip?.Caption;
                return ConvertirMedio(
                    recepcion,
                    tipoMensaje,
                    contenido,
                    mensaje.Type,
                    mensaje.AudioMessageInfobip?.Url);
            case "VOICE":
                tipoMensaje = "audio";
                contenido = mensaje.VoiceMessageInfobip?.Caption;
                return ConvertirMedio(
                    recepcion,
                    tipoMensaje,
                    contenido,
                    mensaje.Type,
                    mensaje.VoiceMessageInfobip?.Url);
            case "VIDEO":
                tipoMensaje = "video";
                contenido = mensaje.VideoMessageInfobip?.Caption;
                return ConvertirMedio(
                    recepcion,
                    tipoMensaje,
                    contenido,
                    mensaje.Type,
                    mensaje.VideoMessageInfobip?.Url);
            default:
                return ResultadoConversionMensajeEntranteInfobip.Fallo(
                    $"El tipo Infobip '{mensaje.Type}' no tiene conversion al modelo generico.");
        }

        return ResultadoConversionMensajeEntranteInfobip.Exito(
            CrearSolicitud(recepcion, tipoMensaje, contenido, archivos));
    }

    private static ResultadoConversionMensajeEntranteInfobip ConvertirMedio(
        WebhookReceiptInfobip recepcion,
        string tipoMensaje,
        string? contenido,
        string tipoInfobip,
        string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return ResultadoConversionMensajeEntranteInfobip.Fallo(
                $"El tipo Infobip '{tipoInfobip}' no contiene una URL absoluta valida.");
        }

        string extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension)
            || !TiposContenido.TryGetValue((tipoInfobip, extension), out string? tipoContenido))
        {
            return ResultadoConversionMensajeEntranteInfobip.Fallo(
                $"La extension '{extension}' no esta admitida para el tipo Infobip '{tipoInfobip}'.");
        }

        DTOArchivoMensaje archivo = new()
        {
            NombreArchivo = Path.GetFileName(uri.AbsolutePath),
            TipoContenido = tipoContenido,
            UbicacionArchivo = url,
            ProveedorAlmacenamiento = "infobip",
            IdentificadorExternoArchivo = recepcion.MessageId
        };

        return ResultadoConversionMensajeEntranteInfobip.Exito(
            CrearSolicitud(recepcion, tipoMensaje, contenido, [archivo]));
    }

    private static DTORegistrarMensajeEntranteSolicitud CrearSolicitud(
        WebhookReceiptInfobip recepcion,
        string tipoMensaje,
        string? contenido,
        List<DTOArchivoMensaje> archivos)
    {
        return new DTORegistrarMensajeEntranteSolicitud
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = recepcion.IntegrationType.Trim().ToLowerInvariant(),
                Cuenta = recepcion.To,
                IdentificadorParticipante = recepcion.From,
                TipoParticipante = "telefono",
                TipoMensaje = tipoMensaje,
                TelefonoOrigen = recepcion.From,
                TelefonoDestino = recepcion.To,
                Contenido = contenido,
                IdentificadorExternoMensaje = recepcion.MessageId,
                FechaMensaje = recepcion.ReceivedAt,
                Archivos = archivos
            }
        };
    }
}
