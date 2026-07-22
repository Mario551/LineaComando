using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class MetadataEntradaContextoIARepositorio : Repositorio<DAOMetadataEntradaContextoIA>, IMetadataEntradaContextoIARepositorio
{
    public MetadataEntradaContextoIARepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
