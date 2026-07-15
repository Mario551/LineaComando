using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class EstadoContextoConversacionRepositorio : Repositorio<DAOEstadoContextoConversacion>, IEstadoContextoConversacionRepositorio
{
    public EstadoContextoConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
