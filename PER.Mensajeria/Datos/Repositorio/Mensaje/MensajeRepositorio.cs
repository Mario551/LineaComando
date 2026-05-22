using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.Mensaje;

public class MensajeRepositorio : Repositorio<DAOMensaje>, IMensajeRepositorio
{
    public MensajeRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
