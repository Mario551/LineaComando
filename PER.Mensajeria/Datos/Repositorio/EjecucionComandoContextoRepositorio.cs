using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class EjecucionComandoContextoRepositorio : Repositorio<DAOEjecucionComandoContexto>, IEjecucionComandoContextoRepositorio
{
    public EjecucionComandoContextoRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
