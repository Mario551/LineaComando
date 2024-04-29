namespace PER.Comandos.LineaComandos.Excepcion
{
    public class StreamCerradoExcepcion : LineaComandosExcepcion
    {
        public StreamCerradoExcepcion() : base() { }
        public StreamCerradoExcepcion(string message) : base(message) { }
        public StreamCerradoExcepcion(string message, Exception innerException) : base(message, innerException) { }
    }
}
