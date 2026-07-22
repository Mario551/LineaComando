using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class InformacionTecnicaLlamadaIALineaConversacionRepositorio : Repositorio<DAOInformacionTecnicaLlamadaIALineaConversacion>, IInformacionTecnicaLlamadaIALineaConversacionRepositorio
{
    public InformacionTecnicaLlamadaIALineaConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
