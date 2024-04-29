namespace PER.Comandos.LineaComandos
{
    public class LineaComando<TDato>
    {
        public string Usuario { get; set; }
        public Linea? Linea { get; set; }
        public uint? IdEjecucion { get; set; }
        public TDato Datos { get; set; }
    }
}
