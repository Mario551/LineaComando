using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.ArchivoMensaje;

public class ArchivoMensajeRepositorio : Repositorio<DAOArchivoMensaje>, IArchivoMensajeRepositorio
{
    public ArchivoMensajeRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
