namespace PER.Comandos.LineaComandos.Excepcion
{
    public class NoEncontradoExcepcion : LineaComandosExcepcion
    {

        public NoEncontradoExcepcion() : base() { }
        public NoEncontradoExcepcion(string message) : base(message) { }
        public NoEncontradoExcepcion(string message, Exception innerException) : base(message, innerException) { }
    }
}
