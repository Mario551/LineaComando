namespace PER.Mensajeria.Entidad.DAO;

public class DAOCuentaCanal
{
    public long ID { get; set; }
    public int IDCanalComunicacion { get; set; }
    public string Cuenta { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Activa { get; set; }
}
