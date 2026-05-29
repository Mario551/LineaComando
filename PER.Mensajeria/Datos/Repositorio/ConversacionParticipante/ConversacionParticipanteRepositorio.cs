using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.ConversacionParticipante;

public class ConversacionParticipanteRepositorio : Repositorio<DAOConversacionParticipante>, IConversacionParticipanteRepositorio
{
    public ConversacionParticipanteRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
