using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.CanalComunicacion;

public class CanalComunicacionRepositorio : Repositorio<DAOCanalComunicacion>, ICanalComunicacionRepositorio
{
    public CanalComunicacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
