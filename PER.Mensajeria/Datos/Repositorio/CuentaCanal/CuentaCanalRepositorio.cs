using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio.CuentaCanal;

public class CuentaCanalRepositorio : Repositorio<DAOCuentaCanal>, ICuentaCanalRepositorio
{
    public CuentaCanalRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
