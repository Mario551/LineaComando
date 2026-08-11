using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Repositorio.Infobip;

public class IntentoEnvioMensajeInfobipRepositorio :
    Repositorio<DAOIntentoEnvioMensajeInfobip>,
    IIntentoEnvioMensajeInfobipRepositorio
{
    public IntentoEnvioMensajeInfobipRepositorio(MensajeriaContextoDB contexto)
        : base(contexto)
    {
    }
}
