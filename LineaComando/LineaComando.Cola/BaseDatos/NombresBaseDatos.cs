namespace PER.Comandos.LineaComandos.Cola.BaseDatos
{
    public sealed class NombresBaseDatos
    {
        private readonly string _apertura;
        private readonly string _cierre;

        private NombresBaseDatos(string esquema, string apertura, string cierre)
        {
            Esquema = ValidarEsquema(esquema);
            _apertura = apertura;
            _cierre = cierre;
        }

        public string Esquema { get; }

        public string EsquemaSql => Delimitar(Esquema);

        public string ComandosRegistrados => Calificar("per_comandos_registrados");

        public string ColaComandosEstados => Calificar("per_cola_comandos_estados");

        public string ColaComandos => Calificar("per_cola_comandos");

        public string ColaComandosResultados => Calificar("per_cola_comandos_resultados");

        public string TiposEvento => Calificar("per_tipos_evento");

        public string ManejadoresEvento => Calificar("per_manejadores_evento");

        public string DisparadoresManejador => Calificar("per_disparadores_manejador");

        public string EventosOutbox => Calificar("per_eventos_outbox");

        public string ObtenerComandosPendientes => Calificar("obtener_comandos_pendientes");

        public string MarcarComandosProcesando => Calificar("marcar_comandos_procesando");

        public string ActualizarFechaLeido => Calificar("actualizar_fecha_leido");

        public string ObtenerEventosPendientes => Calificar("obtener_eventos_pendientes");

        public static NombresBaseDatos Postgres(string? esquema = null)
        {
            return new NombresBaseDatos(NormalizarEsquema(esquema, "public"), "\"", "\"");
        }

        public static NombresBaseDatos SqlServer(string? esquema = null)
        {
            return new NombresBaseDatos(NormalizarEsquema(esquema, "dbo"), "[", "]");
        }

        public static string NormalizarEsquema(string? esquema, string predeterminado)
        {
            return string.IsNullOrWhiteSpace(esquema)
                ? ValidarEsquema(predeterminado)
                : ValidarEsquema(esquema);
        }

        private string Calificar(string objeto)
        {
            return $"{EsquemaSql}.{Delimitar(objeto)}";
        }

        private string Delimitar(string identificador)
        {
            return $"{_apertura}{identificador}{_cierre}";
        }

        private static string ValidarEsquema(string esquema)
        {
            if (string.IsNullOrWhiteSpace(esquema))
                throw new ArgumentException("El esquema no puede estar vacío.", nameof(esquema));

            if (!EsInicioIdentificadorValido(esquema[0]))
                throw new ArgumentException($"El esquema '{esquema}' no es válido.", nameof(esquema));

            for (int i = 1; i < esquema.Length; i++)
            {
                if (!EsParteIdentificadorValida(esquema[i]))
                    throw new ArgumentException($"El esquema '{esquema}' no es válido.", nameof(esquema));
            }

            return esquema;
        }

        private static bool EsInicioIdentificadorValido(char caracter)
        {
            return caracter == '_' || char.IsLetter(caracter);
        }

        private static bool EsParteIdentificadorValida(char caracter)
        {
            return caracter == '_' || char.IsLetterOrDigit(caracter);
        }
    }
}
