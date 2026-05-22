using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.EnvioMensaje;

public class EnvioMensajeRepositorio : Repositorio<DAOEnvioMensaje>, IEnvioMensajeRepositorio
{
    public EnvioMensajeRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
