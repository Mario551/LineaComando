using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class CompactacionContextoConversacionRepositorio : Repositorio<DAOCompactacionContextoConversacion>, ICompactacionContextoConversacionRepositorio
{
    public CompactacionContextoConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
