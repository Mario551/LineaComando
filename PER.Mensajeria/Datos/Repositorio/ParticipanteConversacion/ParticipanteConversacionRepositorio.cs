using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.ParticipanteConversacion;

public class ParticipanteConversacionRepositorio : Repositorio<DAOParticipanteConversacion>, IParticipanteConversacionRepositorio
{
    public ParticipanteConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
