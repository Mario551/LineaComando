namespace PER.Comandos.LineaComandos.Excepcion
{
    public class ColeccionVaciaExcepcion : LineaComandosExcepcion
    {
        public ColeccionVaciaExcepcion() : base() { }
        public ColeccionVaciaExcepcion(string message) : base(message) { }
        public ColeccionVaciaExcepcion(string message, Exception innerException) : base(message, innerException) { }
    }
}
