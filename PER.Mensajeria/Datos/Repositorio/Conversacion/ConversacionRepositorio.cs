using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.Conversacion;

public class ConversacionRepositorio : Repositorio<DAOConversacion>, IConversacionRepositorio
{
    public ConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
