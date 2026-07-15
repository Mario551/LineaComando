using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Repositorio;

public class MetadataRazonamientoIALineaConversacionRepositorio : Repositorio<DAOMetadataRazonamientoIALineaConversacion>, IMetadataRazonamientoIALineaConversacionRepositorio
{
    public MetadataRazonamientoIALineaConversacionRepositorio(MensajeriaContextoDB contexto) : base(contexto)
    {
    }
}
