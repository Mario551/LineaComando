namespace PER.Comandos.LineaComandos.Excepcion
{
    public class ErrorDeSintaxisExcepcion : LineaComandosExcepcion
    {
        public ErrorDeSintaxisExcepcion() : base() { }
        public ErrorDeSintaxisExcepcion(string message) : base(message) { }
        public ErrorDeSintaxisExcepcion(string message, Exception innerException) : base(message, innerException) { }
    }
}
