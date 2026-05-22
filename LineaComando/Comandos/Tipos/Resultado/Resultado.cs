namespace PER.Comandos.Tipos.Resultado
{
    public class Resultado<T>
    {
        public T? Res { get; set; }
        public bool Ok { get; set; }
        public Error? Error { get; set; }

        public Resultado(bool ok, T? res = default)
        {
            Res = res;
            Ok = ok;
            Error = null;
        }
    }

    public class Resultado
    {
        public bool Ok { get; set; }
        public Error? Error { get; set; }

        public Resultado(bool ok, Error? error = null)
        {
            Ok = ok;
            Error = error;
        }
    }
}
