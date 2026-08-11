using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.API.Infobip;

public interface IAdaptadorMensajeSalidaInfobip
{
    DTOResultadoAdaptacionEnvioInfobip Adaptar(
        DTOEnvioMensajePendiente mensaje);
}
