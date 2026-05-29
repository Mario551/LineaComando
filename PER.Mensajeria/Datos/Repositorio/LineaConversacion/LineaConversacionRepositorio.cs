using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.LineaConversacion;

public class LineaConversacionRepositorio : Repositorio<DAOLineaConversacion>, ILineaConversacionRepositorio
{
    public LineaConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
