using PER.Comandos.Tipos.Resultado;

namespace PER.Comandos.LineaComandos
{
    public class RespuestaComando<TDato>
    {
        public int Codigo { get; set; }
        public uint? IdEjecucion { get; set; }
        public Linea? Linea { get; set; }
        public Error? Error { get; set; }
        public TDato Datos { get; set; }
    }
}
