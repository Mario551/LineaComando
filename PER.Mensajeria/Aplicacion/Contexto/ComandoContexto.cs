namespace PER.Mensajeria.Aplicacion.Contexto;

public class ComandoContexto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Alcance { get; set; } = string.Empty;
    public string ReglasUso { get; set; } = string.Empty;
    public bool Autorizado { get; set; } = true;
    public Dictionary<string, string> Parametros { get; set; } = [];
}
