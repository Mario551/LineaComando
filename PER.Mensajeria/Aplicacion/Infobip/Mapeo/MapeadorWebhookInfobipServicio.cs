using System.Globalization;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.Mapeo;

public class MapeadorWebhookInfobipServicio : IMapeadorWebhookInfobipServicio
{
    private static readonly HashSet<string> TiposSoportados = new(StringComparer.Ordinal)
    {
        "TEXT",
        "LOCATION",
        "IMAGE",
        "DOCUMENT",
        "AUDIO",
        "VIDEO",
        "VOICE",
        "CONTACT",
        "INFECTED_CONTENT",
        "BUTTON",
        "STICKER",
        "INTERACTIVE_BUTTON_REPLY",
        "INTERACTIVE_LIST_REPLY",
        "INTERACTIVE_FLOW_REPLY",
        "INTERACTIVE_PAYMENT_CONFIRMATION",
        "INTERACTIVE_CALL_PERMISSION_REPLY",
        "INTERACTIVE_IN_THREAD_AUTHENTICATION_REPLY",
        "ORDER",
        "REACTION",
        "UNSUPPORTED"
    };

    public WebhookReceiptInfobip Mapear(
        DTOInfobipResult resultado,
        DateTime fechaCreacion)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        DTOInfobipMessage mensaje = resultado.Message
            ?? throw new InvalidOperationException("El resultado Infobip no contiene message.");
        DTOInfobipMessagePrice precio = resultado.Price
            ?? throw new InvalidOperationException("El resultado Infobip no contiene price.");
        DTOInfobipContactProfile contacto = resultado.Contact
            ?? throw new InvalidOperationException("El resultado Infobip no contiene contact.");
        string tipo = ObtenerTipo(mensaje.Type);

        WebhookReceiptInfobip recepcion = new()
        {
            EntityId = resultado.EntityId,
            ApplicationId = resultado.ApplicationId,
            From = ObtenerObligatorio(resultado.From, "from"),
            To = ObtenerObligatorio(resultado.To, "to"),
            IntegrationType = ObtenerObligatorio(resultado.IntegrationType, "integrationType"),
            ReceivedAt = ObtenerFecha(resultado.ReceivedAt, "receivedAt"),
            Keyword = resultado.Keyword,
            MessageId = ObtenerObligatorio(resultado.MessageId, "messageId"),
            PairedMessageId = resultado.PairedMessageId,
            CallbackData = resultado.CallbackData,
            PricePerMessage = precio.PricePerMessage,
            Currency = precio.Currency,
            Name = contacto.Name,
            PhoneNumber = contacto.PhoneNumber,
            UserId = contacto.UserId,
            ParentUserId = contacto.ParentUserId,
            Username = contacto.Username,
            RecordCreatedAt = fechaCreacion
        };

        if (resultado.Identity is not null)
        {
            recepcion.Acknowledged = resultado.Identity.Acknowledged;
            recepcion.Hash = ObtenerObligatorio(resultado.Identity.Hash, "identity.hash");
            recepcion.CreatedAt = ObtenerFecha(resultado.Identity.CreatedAt, "identity.createdAt");
        }

        InboundMessageInfobip mensajeEntrante = new()
        {
            Type = tipo,
            RecordCreatedAt = fechaCreacion,
            WebhookReceiptInfobip = recepcion
        };

        recepcion.InboundMessageInfobip = mensajeEntrante;
        MapearContexto(mensajeEntrante, mensaje.Context, fechaCreacion);
        MapearReferral(mensajeEntrante, mensaje.Referral, fechaCreacion);
        MapearContenido(mensajeEntrante, mensaje, tipo, fechaCreacion);
        return recepcion;
    }

    private static void MapearContenido(
        InboundMessageInfobip destino,
        DTOInfobipMessage origen,
        string tipo,
        DateTime fechaCreacion)
    {
        switch (tipo)
        {
            case "TEXT":
                destino.TextMessageInfobip = new TextMessageInfobip
                {
                    Text = ObtenerObligatorio(origen.Text, "message.text"),
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "LOCATION":
                destino.LocationMessageInfobip = new LocationMessageInfobip
                {
                    Latitude = origen.Latitude
                        ?? throw new InvalidOperationException("LOCATION requiere latitude."),
                    Longitude = origen.Longitude
                        ?? throw new InvalidOperationException("LOCATION requiere longitude."),
                    Address = origen.Address,
                    Name = origen.Name,
                    Url = origen.Url,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "IMAGE":
                destino.ImageMessageInfobip = new ImageMessageInfobip
                {
                    Url = ObtenerObligatorio(origen.Url, "message.url"),
                    Caption = origen.Caption,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "DOCUMENT":
                destino.DocumentMessageInfobip = new DocumentMessageInfobip
                {
                    Url = ObtenerObligatorio(origen.Url, "message.url"),
                    Caption = origen.Caption,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "AUDIO":
                destino.AudioMessageInfobip = new AudioMessageInfobip
                {
                    Url = ObtenerObligatorio(origen.Url, "message.url"),
                    Caption = origen.Caption,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "VIDEO":
                destino.VideoMessageInfobip = new VideoMessageInfobip
                {
                    Url = ObtenerObligatorio(origen.Url, "message.url"),
                    Caption = origen.Caption,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "VOICE":
                destino.VoiceMessageInfobip = new VoiceMessageInfobip
                {
                    Url = ObtenerObligatorio(origen.Url, "message.url"),
                    Caption = origen.Caption,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "CONTACT":
                destino.ContactMessageInfobip = MapearContactos(destino, origen, fechaCreacion);
                break;
            case "INFECTED_CONTENT":
                destino.InfectedContentMessageInfobip = new InfectedContentMessageInfobip
                {
                    Malware = origen.Malware,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "BUTTON":
                destino.ButtonMessageInfobip = new ButtonMessageInfobip
                {
                    Text = ObtenerObligatorio(origen.Text, "message.text"),
                    Payload = ObtenerObligatorio(origen.Payload, "message.payload"),
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "STICKER":
                destino.StickerMessageInfobip = new StickerMessageInfobip
                {
                    Url = ObtenerObligatorio(origen.Url, "message.url"),
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "INTERACTIVE_BUTTON_REPLY":
                destino.InteractiveButtonReplyMessageInfobip = new InteractiveButtonReplyMessageInfobip
                {
                    Id = ObtenerObligatorio(origen.Id, "message.id"),
                    Title = ObtenerObligatorio(origen.Title, "message.title"),
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "INTERACTIVE_LIST_REPLY":
                destino.InteractiveListReplyMessageInfobip = new InteractiveListReplyMessageInfobip
                {
                    Id = ObtenerObligatorio(origen.Id, "message.id"),
                    Title = ObtenerObligatorio(origen.Title, "message.title"),
                    Description = origen.Description,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "INTERACTIVE_FLOW_REPLY":
                destino.FlowReplyMessageInfobip = MapearFlow(destino, origen, fechaCreacion);
                break;
            case "INTERACTIVE_PAYMENT_CONFIRMATION":
                destino.PaymentConfirmationMessageInfobip = MapearPago(destino, origen, fechaCreacion);
                break;
            case "INTERACTIVE_CALL_PERMISSION_REPLY":
                destino.CallPermissionReplyMessageInfobip = MapearPermisoLlamada(destino, origen, fechaCreacion);
                break;
            case "INTERACTIVE_IN_THREAD_AUTHENTICATION_REPLY":
                destino.InThreadAuthenticationReplyMessageInfobip = MapearAutenticacion(destino, origen, fechaCreacion);
                break;
            case "ORDER":
                destino.OrderMessageInfobip = MapearOrden(destino, origen, fechaCreacion);
                break;
            case "REACTION":
                destino.ReactionMessageInfobip = new ReactionMessageInfobip
                {
                    Emoji = origen.Emoji,
                    Action = origen.Action,
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            case "UNSUPPORTED":
                destino.UnsupportedMessageInfobip = new UnsupportedMessageInfobip
                {
                    RecordCreatedAt = fechaCreacion,
                    InboundMessageInfobip = destino
                };
                break;
            default:
                throw new InvalidOperationException($"El tipo Infobip '{tipo}' no esta soportado.");
        }
    }

    private static ContactMessageInfobip MapearContactos(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipMessage mensaje,
        DateTime fechaCreacion)
    {
        List<DTOInfobipSharedContact> contactos = mensaje.Contacts
            ?? throw new InvalidOperationException("CONTACT requiere contacts.");
        ContactMessageInfobip destino = new()
        {
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };

        for (int indice = 0; indice < contactos.Count; indice++)
        {
            DTOInfobipSharedContact contacto = contactos[indice];
            SharedContactInfobip contactoDestino = new()
            {
                ContactIndex = indice,
                Birthday = ObtenerFechaNacimiento(contacto.Birthday),
                FirstName = contacto.Name?.FirstName,
                LastName = contacto.Name?.LastName,
                MiddleName = contacto.Name?.MiddleName,
                NameSuffix = contacto.Name?.NameSuffix,
                NamePrefix = contacto.Name?.NamePrefix,
                FormattedName = contacto.Name?.FormattedName,
                Company = contacto.Org?.Company,
                Department = contacto.Org?.Department,
                Title = contacto.Org?.Title,
                RecordCreatedAt = fechaCreacion,
                ContactMessageInfobip = destino
            };

            MapearDirecciones(contactoDestino, contacto.Addresses, fechaCreacion);
            MapearCorreos(contactoDestino, contacto.Emails, fechaCreacion);
            MapearTelefonos(contactoDestino, contacto.Phones, fechaCreacion);
            MapearUrls(contactoDestino, contacto.Urls, fechaCreacion);
            destino.SharedContactsInfobip.Add(contactoDestino);
        }

        return destino;
    }

    private static void MapearDirecciones(
        SharedContactInfobip destino,
        IReadOnlyList<DTOInfobipContactAddress>? direcciones,
        DateTime fechaCreacion)
    {
        if (direcciones is null)
        {
            return;
        }

        for (int indice = 0; indice < direcciones.Count; indice++)
        {
            DTOInfobipContactAddress origen = direcciones[indice];
            destino.ContactAddressesInfobip.Add(new ContactAddressInfobip
            {
                AddressIndex = indice,
                Street = origen.Street,
                City = origen.City,
                State = origen.State,
                Zip = origen.Zip,
                Country = origen.Country,
                CountryCode = origen.CountryCode,
                Type = origen.Type,
                RecordCreatedAt = fechaCreacion,
                SharedContactInfobip = destino
            });
        }
    }

    private static void MapearCorreos(
        SharedContactInfobip destino,
        IReadOnlyList<DTOInfobipContactEmail>? correos,
        DateTime fechaCreacion)
    {
        if (correos is null)
        {
            return;
        }

        for (int indice = 0; indice < correos.Count; indice++)
        {
            DTOInfobipContactEmail origen = correos[indice];
            destino.ContactEmailsInfobip.Add(new ContactEmailInfobip
            {
                EmailIndex = indice,
                Email = origen.Email,
                Type = origen.Type,
                RecordCreatedAt = fechaCreacion,
                SharedContactInfobip = destino
            });
        }
    }

    private static void MapearTelefonos(
        SharedContactInfobip destino,
        IReadOnlyList<DTOInfobipContactPhone>? telefonos,
        DateTime fechaCreacion)
    {
        if (telefonos is null)
        {
            return;
        }

        for (int indice = 0; indice < telefonos.Count; indice++)
        {
            DTOInfobipContactPhone origen = telefonos[indice];
            destino.ContactPhonesInfobip.Add(new ContactPhoneInfobip
            {
                PhoneIndex = indice,
                Phone = origen.Phone,
                Type = origen.Type,
                WaId = origen.WaId,
                RecordCreatedAt = fechaCreacion,
                SharedContactInfobip = destino
            });
        }
    }

    private static void MapearUrls(
        SharedContactInfobip destino,
        IReadOnlyList<DTOInfobipContactUrl>? urls,
        DateTime fechaCreacion)
    {
        if (urls is null)
        {
            return;
        }

        for (int indice = 0; indice < urls.Count; indice++)
        {
            DTOInfobipContactUrl origen = urls[indice];
            destino.ContactUrlsInfobip.Add(new ContactUrlInfobip
            {
                UrlIndex = indice,
                Url = origen.Url,
                Type = origen.Type,
                RecordCreatedAt = fechaCreacion,
                SharedContactInfobip = destino
            });
        }
    }

    private static FlowReplyMessageInfobip MapearFlow(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipMessage mensaje,
        DateTime fechaCreacion)
    {
        FlowReplyMessageInfobip destino = new()
        {
            Text = ObtenerObligatorio(mensaje.Text, "message.text"),
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };

        foreach (DTOInfobipFlowResponseNode nodo in mensaje.Response)
        {
            destino.FlowResponseNodesInfobip.Add(
                MapearNodoFlow(destino, null, nodo, fechaCreacion));
        }

        return destino;
    }

    private static FlowResponseNodeInfobip MapearNodoFlow(
        FlowReplyMessageInfobip flow,
        FlowResponseNodeInfobip? padre,
        DTOInfobipFlowResponseNode origen,
        DateTime fechaCreacion)
    {
        FlowResponseNodeInfobip destino = new()
        {
            Key = origen.Key,
            ElementIndex = origen.ElementIndex,
            NodeType = ObtenerObligatorio(origen.NodeType, "response.nodeType"),
            TextValue = origen.TextValue,
            NumericValue = origen.NumericValue,
            BooleanValue = origen.BooleanValue,
            RecordCreatedAt = fechaCreacion,
            FlowReplyMessageInfobip = flow,
            Parent = padre
        };

        foreach (DTOInfobipFlowResponseNode hijo in origen.Children)
        {
            destino.Children.Add(MapearNodoFlow(flow, destino, hijo, fechaCreacion));
        }

        return destino;
    }

    private static PaymentConfirmationMessageInfobip MapearPago(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipMessage mensaje,
        DateTime fechaCreacion)
    {
        DTOInfobipPaymentAmount total = mensaje.TotalAmount
            ?? throw new InvalidOperationException(
                "INTERACTIVE_PAYMENT_CONFIRMATION requiere totalAmount.");

        return new PaymentConfirmationMessageInfobip
        {
            ReferenceId = ObtenerObligatorio(mensaje.ReferenceId, "message.referenceId"),
            PaymentId = mensaje.PaymentId,
            Status = ObtenerObligatorio(mensaje.Status, "message.status"),
            Currency = ObtenerObligatorio(mensaje.Currency, "message.currency"),
            Value = total.Value,
            Offset = total.Offset,
            TransactionId = ObtenerObligatorio(mensaje.TransactionId, "message.transactionId"),
            TransactionType = ObtenerObligatorio(mensaje.TransactionType, "message.transactionType"),
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };
    }

    private static CallPermissionReplyMessageInfobip MapearPermisoLlamada(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipMessage mensaje,
        DateTime fechaCreacion)
    {
        DTOInfobipCallPermissionReply respuesta = mensaje.CallPermissionReply
            ?? throw new InvalidOperationException(
                "INTERACTIVE_CALL_PERMISSION_REPLY requiere callPermissionReply.");

        return new CallPermissionReplyMessageInfobip
        {
            Response = ObtenerObligatorio(respuesta.Response, "callPermissionReply.response"),
            ExpirationTimestamp = ObtenerFechaOpcional(
                respuesta.ExpirationTimestamp,
                "callPermissionReply.expirationTimestamp"),
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };
    }

    private static InThreadAuthenticationReplyMessageInfobip MapearAutenticacion(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipMessage mensaje,
        DateTime fechaCreacion)
    {
        DTOInfobipInThreadAuthenticationReply respuesta = mensaje.InThreadAuthenticationReply
            ?? throw new InvalidOperationException(
                "INTERACTIVE_IN_THREAD_AUTHENTICATION_REPLY requiere inThreadAuthenticationReply.");

        return new InThreadAuthenticationReplyMessageInfobip
        {
            Status = ObtenerObligatorio(respuesta.Status, "inThreadAuthenticationReply.status"),
            BusinessScopedPasskeyHash = respuesta.BusinessScopedPasskeyHash,
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };
    }

    private static OrderMessageInfobip MapearOrden(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipMessage mensaje,
        DateTime fechaCreacion)
    {
        List<DTOInfobipOrderProductItem> productos = mensaje.ProductItems
            ?? throw new InvalidOperationException("ORDER requiere productItems.");
        OrderMessageInfobip destino = new()
        {
            CatalogId = ObtenerObligatorio(mensaje.CatalogId, "message.catalogId"),
            Text = mensaje.Text,
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };

        for (int indice = 0; indice < productos.Count; indice++)
        {
            DTOInfobipOrderProductItem producto = productos[indice];
            destino.OrderProductItemsInfobip.Add(new OrderProductItemInfobip
            {
                ProductItemIndex = indice,
                Currency = ObtenerObligatorio(producto.Currency, "productItem.currency"),
                ItemPrice = producto.ItemPrice,
                ProductRetailerId = ObtenerObligatorio(
                    producto.ProductRetailerId,
                    "productItem.productRetailerId"),
                Quantity = producto.Quantity,
                RecordCreatedAt = fechaCreacion,
                OrderMessageInfobip = destino
            });
        }

        return destino;
    }

    private static void MapearContexto(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipContext? contexto,
        DateTime fechaCreacion)
    {
        if (contexto is null)
        {
            return;
        }

        mensajeEntrante.MessageContextInfobip = new MessageContextInfobip
        {
            From = contexto.From,
            Id = contexto.Id,
            GroupId = contexto.GroupId,
            CatalogId = contexto.ReferredProduct?.CatalogId,
            ProductRetailerId = contexto.ReferredProduct?.ProductRetailerId,
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };
    }

    private static void MapearReferral(
        InboundMessageInfobip mensajeEntrante,
        DTOInfobipReferral? referral,
        DateTime fechaCreacion)
    {
        if (referral is null)
        {
            return;
        }

        mensajeEntrante.MessageReferralInfobip = new MessageReferralInfobip
        {
            SourceType = ObtenerObligatorio(referral.SourceType, "referral.sourceType"),
            SourceId = referral.SourceId,
            SourceUrl = ObtenerObligatorio(referral.SourceUrl, "referral.sourceUrl"),
            Headline = referral.Headline,
            Body = referral.Body,
            Type = referral.ReferralMedia?.Type,
            Url = referral.ReferralMedia?.Url,
            CtwaClickId = referral.CtwaClickId,
            RecordCreatedAt = fechaCreacion,
            InboundMessageInfobip = mensajeEntrante
        };
    }

    private static string ObtenerTipo(string tipo)
    {
        string tipoNormalizado = ObtenerObligatorio(tipo, "message.type")
            .Trim()
            .ToUpperInvariant();

        if (!TiposSoportados.Contains(tipoNormalizado))
        {
            throw new InvalidOperationException(
                $"El tipo Infobip '{tipoNormalizado}' no pertenece al catalogo soportado.");
        }

        return tipoNormalizado;
    }

    private static string ObtenerObligatorio(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException($"El campo Infobip '{campo}' es obligatorio.");
        }

        return valor;
    }

    private static DateTime ObtenerFecha(string valor, string campo)
    {
        if (!DateTimeOffset.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset fecha))
        {
            throw new InvalidOperationException(
                $"El campo Infobip '{campo}' no contiene una fecha valida.");
        }

        return DateTime.SpecifyKind(fecha.UtcDateTime, DateTimeKind.Unspecified);
    }

    private static DateTime? ObtenerFechaOpcional(string? valor, string campo)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : ObtenerFecha(valor, campo);
    }

    private static DateOnly? ObtenerFechaNacimiento(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (!DateOnly.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly fecha))
        {
            throw new InvalidOperationException(
                "El campo Infobip 'contact.birthday' no contiene una fecha valida.");
        }

        return fecha;
    }
}
