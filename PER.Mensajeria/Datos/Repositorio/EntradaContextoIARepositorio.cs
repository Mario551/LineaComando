using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class EntradaContextoIARepositorio : Repositorio<DAOEntradaContextoIA>, IEntradaContextoIARepositorio
{
    public EntradaContextoIARepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
