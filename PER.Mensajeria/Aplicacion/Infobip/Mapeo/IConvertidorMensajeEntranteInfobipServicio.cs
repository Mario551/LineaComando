using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Aplicacion.Infobip.Mapeo;

public interface IConvertidorMensajeEntranteInfobipServicio
{
    ResultadoConversionMensajeEntranteInfobip Convertir(
        WebhookReceiptInfobip recepcion);
}
