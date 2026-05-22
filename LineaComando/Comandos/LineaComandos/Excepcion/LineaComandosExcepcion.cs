namespace PER.Comandos.LineaComandos.Excepcion
{
    public class LineaComandosExcepcion : Exception
    {
        public LineaComandosExcepcion() : base() { }
        public LineaComandosExcepcion(string message) : base(message) { }
        public LineaComandosExcepcion(string message, Exception innerException) : base(message, innerException) { }
    }
}
