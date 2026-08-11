using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Repositorio.Infobip;

public class ProcesamientoMensajeEntranteInfobipRepositorio :
    Repositorio<DAOProcesamientoMensajeEntranteInfobip>,
    IProcesamientoMensajeEntranteInfobipRepositorio
{
    public ProcesamientoMensajeEntranteInfobipRepositorio(MensajeriaContextoDB contexto)
        : base(contexto)
    {
    }
}
