using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.ProcesamientoInternoMensaje;

public class ProcesamientoInternoMensajeRepositorio : Repositorio<DAOProcesamientoInternoMensaje>, IProcesamientoInternoMensajeRepositorio
{
    public ProcesamientoInternoMensajeRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
