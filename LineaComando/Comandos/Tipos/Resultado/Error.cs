namespace PER.Comandos.Tipos.Resultado
{
    public class Error
    {
        public int? Codigo { get; set; }
        public string Mensaje { get; set; }

        public Error()
        {
            Codigo = null;
            Mensaje = string.Empty;
        }

        public Error(string mensaje, int? codigo = null)
        {
            Codigo = codigo;
            Mensaje = mensaje;
        }
    }
}
