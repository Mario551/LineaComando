namespace PER.Comandos.LineaComandos.Cola.DAO
{
    public class ComandoRegistrado
    {
        public int Id { get; set; }
        public string RutaComando { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? EsquemaParametros { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; }
        public DateTime? ActualizadoEn { get; set; }
    }
}
