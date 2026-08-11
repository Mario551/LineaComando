using System.Globalization;
using System.Text.Json;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.API.Infobip;

public class AdaptadorMensajeSalidaInfobip : IAdaptadorMensajeSalidaInfobip
{
    private const string CanalWhatsApp = "whatsapp";
    private const string TipoDestinatarioTelefono = "telefono";

    public DTOResultadoAdaptacionEnvioInfobip Adaptar(
        DTOEnvioMensajePendiente mensaje)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        string? errorComun = ValidarDatosComunes(mensaje);
        if (errorComun is not null)
        {
            return Fallo(errorComun);
        }

        return mensaje.Mensaje.TipoMensaje switch
        {
            "texto" => AdaptarTexto(mensaje),
            "imagen" => AdaptarMedio(mensaje, "image", admiteCaption: true, admiteNombre: false),
            "audio" => AdaptarMedio(mensaje, "audio", admiteCaption: false, admiteNombre: false),
            "video" => AdaptarMedio(mensaje, "video", admiteCaption: true, admiteNombre: false),
            "documento" => AdaptarMedio(mensaje, "document", admiteCaption: true, admiteNombre: true),
            "ubicacion" => AdaptarUbicacion(mensaje),
            _ => Fallo(
                $"El tipo de mensaje '{mensaje.Mensaje.TipoMensaje}' no está soportado por el adaptador Infobip.")
        };
    }

    private static string? ValidarDatosComunes(DTOEnvioMensajePendiente mensaje)
    {
        if (mensaje.IDEnvioMensaje <= 0)
        {
            return "El identificador del envío debe ser mayor que cero.";
        }

        if (!string.Equals(mensaje.Canal, CanalWhatsApp, StringComparison.OrdinalIgnoreCase))
        {
            return $"El canal '{mensaje.Canal}' no está soportado por Infobip WhatsApp.";
        }

        if (string.IsNullOrWhiteSpace(mensaje.Cuenta))
        {
            return "La cuenta emisora de Infobip es obligatoria.";
        }

        if (!string.Equals(
                mensaje.TipoDestinatario,
                TipoDestinatarioTelefono,
                StringComparison.OrdinalIgnoreCase))
        {
            return $"El tipo de destinatario '{mensaje.TipoDestinatario}' no está soportado por Infobip.";
        }

        if (string.IsNullOrWhiteSpace(mensaje.IdentificadorDestinatario))
        {
            return "El identificador del destinatario es obligatorio.";
        }

        return null;
    }

    private static DTOResultadoAdaptacionEnvioInfobip AdaptarTexto(
        DTOEnvioMensajePendiente mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje.Mensaje.Contenido))
        {
            return Fallo("El mensaje de texto no contiene texto para enviar.");
        }

        if (mensaje.Mensaje.Archivos.Count > 0)
        {
            return Fallo("El mensaje de texto no puede contener archivos.");
        }

        DTOInfobipEnvioTextoSolicitud cuerpo = new()
        {
            From = mensaje.Cuenta,
            To = mensaje.IdentificadorDestinatario,
            CallbackData = mensaje.IDEnvioMensaje.ToString(CultureInfo.InvariantCulture),
            Content = new DTOInfobipContenidoTexto
            {
                Text = mensaje.Mensaje.Contenido
            }
        };

        return Exito("/whatsapp/1/message/text", cuerpo);
    }

    private static DTOResultadoAdaptacionEnvioInfobip AdaptarMedio(
        DTOEnvioMensajePendiente mensaje,
        string endpoint,
        bool admiteCaption,
        bool admiteNombre)
    {
        if (mensaje.Mensaje.Archivos.Count != 1)
        {
            return Fallo(
                $"El tipo '{mensaje.Mensaje.TipoMensaje}' requiere exactamente un archivo.");
        }

        DTOArchivoMensaje archivo = mensaje.Mensaje.Archivos[0];
        if (!Uri.TryCreate(archivo.UbicacionArchivo, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return Fallo(
                $"El archivo del tipo '{mensaje.Mensaje.TipoMensaje}' debe tener una URL HTTPS absoluta.");
        }

        DTOInfobipEnvioMedioSolicitud cuerpo = new()
        {
            From = mensaje.Cuenta,
            To = mensaje.IdentificadorDestinatario,
            CallbackData = mensaje.IDEnvioMensaje.ToString(CultureInfo.InvariantCulture),
            Content = new DTOInfobipContenidoMedio
            {
                MediaUrl = uri.AbsoluteUri,
                Caption = admiteCaption && !string.IsNullOrWhiteSpace(mensaje.Mensaje.Contenido)
                    ? mensaje.Mensaje.Contenido
                    : null,
                Filename = admiteNombre && !string.IsNullOrWhiteSpace(archivo.NombreArchivo)
                    ? archivo.NombreArchivo
                    : null
            }
        };

        return Exito($"/whatsapp/1/message/{endpoint}", cuerpo);
    }

    private static DTOResultadoAdaptacionEnvioInfobip AdaptarUbicacion(
        DTOEnvioMensajePendiente mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje.Mensaje.Contenido))
        {
            return Fallo("El mensaje de ubicación no contiene coordenadas.");
        }

        if (mensaje.Mensaje.Archivos.Count > 0)
        {
            return Fallo("El mensaje de ubicación no puede contener archivos.");
        }

        DTOInfobipContenidoUbicacion? ubicacion;

        try
        {
            ubicacion = JsonSerializer.Deserialize<DTOInfobipContenidoUbicacion>(
                mensaje.Mensaje.Contenido,
                InfobipSerializacion.Opciones);
        }
        catch (JsonException excepcion)
        {
            return Fallo($"El contenido de ubicación no es JSON válido: {excepcion.Message}");
        }

        if (ubicacion?.Latitude is null
            || ubicacion.Longitude is null
            || ubicacion.Latitude is < -90 or > 90
            || ubicacion.Longitude is < -180 or > 180)
        {
            return Fallo("Las coordenadas del mensaje de ubicación no son válidas.");
        }

        DTOInfobipEnvioUbicacionSolicitud cuerpo = new()
        {
            From = mensaje.Cuenta,
            To = mensaje.IdentificadorDestinatario,
            CallbackData = mensaje.IDEnvioMensaje.ToString(CultureInfo.InvariantCulture),
            Content = ubicacion
        };

        return Exito("/whatsapp/1/message/location", cuerpo);
    }

    private static DTOResultadoAdaptacionEnvioInfobip Exito<T>(
        string endpoint,
        T cuerpo)
    {
        return new DTOResultadoAdaptacionEnvioInfobip
        {
            Exitosa = true,
            Solicitud = new DTOInfobipSolicitudEnvio
            {
                Endpoint = endpoint,
                CuerpoJson = JsonSerializer.Serialize(cuerpo, InfobipSerializacion.Opciones)
            }
        };
    }

    private static DTOResultadoAdaptacionEnvioInfobip Fallo(string error)
    {
        return new DTOResultadoAdaptacionEnvioInfobip
        {
            Exitosa = false,
            Error = error
        };
    }
}
