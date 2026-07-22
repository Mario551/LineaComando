namespace PER.Mensajeria.Datos.Esquema;

public sealed class NombresBaseDatosMensajeria
{
    private readonly string apertura;
    private readonly string cierre;

    private NombresBaseDatosMensajeria(string esquema, string apertura, string cierre)
    {
        Esquema = ValidarEsquema(esquema);
        this.apertura = apertura;
        this.cierre = cierre;
    }

    public string Esquema { get; }
    public string EsquemaSql => Delimitar(Esquema);
    public string CanalesComunicacion => Calificar("per_canales_comunicacion");
    public string TiposParticipanteConversacion => Calificar("per_tipos_participante_conversacion");
    public string TiposMensaje => Calificar("per_tipos_mensaje");
    public string DireccionesMensaje => Calificar("per_direcciones_mensaje");
    public string TiposContenidoArchivo => Calificar("per_tipos_contenido_archivo");
    public string TiposProcesamientoInternoMensaje => Calificar("per_tipos_procesamiento_interno_mensaje");
    public string EstadosProcesamientoInternoMensaje => Calificar("per_estados_procesamiento_interno_mensaje");
    public string EstadosEnvioMensaje => Calificar("per_estados_envio_mensaje");
    public string RolesContextoIA => Calificar("per_roles_contexto_ia");
    public string TiposEntradaContextoIA => Calificar("per_tipos_entrada_contexto_ia");
    public string CuentasCanal => Calificar("per_cuentas_canal");
    public string ParticipantesConversacion => Calificar("per_participantes_conversacion");
    public string Conversaciones => Calificar("per_conversaciones");
    public string ConversacionesParticipantes => Calificar("per_conversaciones_participantes");
    public string LineasConversacion => Calificar("per_lineas_conversacion");
    public string Mensajes => Calificar("per_mensajes");
    public string ArchivosMensaje => Calificar("per_archivos_mensaje");
    public string ProcesamientosInternosMensaje => Calificar("per_procesamientos_internos_mensaje");
    public string MetadataEntradasContextoIA => Calificar("per_metadata_entradas_contexto_ia");
    public string InformacionTecnicaLlamadasIALineaConversacion => Calificar("per_informacion_tecnica_llamadas_ia_linea_conversacion");
    public string CompactacionesContextoConversacion => Calificar("per_compactaciones_contexto_conversacion");
    public string EstadosEjecucionComandoContexto => Calificar("per_estados_ejecucion_comando_contexto");
    public string EjecucionesComandoContexto => Calificar("per_ejecuciones_comando_contexto");
    public string EnviosMensaje => Calificar("per_envios_mensaje");

    public static NombresBaseDatosMensajeria Postgres(string? esquema = null)
    {
        return new NombresBaseDatosMensajeria(NormalizarEsquema(esquema, "public"), "\"", "\"");
    }

    public static NombresBaseDatosMensajeria SqlServer(string? esquema = null)
    {
        return new NombresBaseDatosMensajeria(NormalizarEsquema(esquema, "dbo"), "[", "]");
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
        return $"{apertura}{identificador}{cierre}";
    }

    private static string ValidarEsquema(string esquema)
    {
        if (string.IsNullOrWhiteSpace(esquema))
        {
            throw new ArgumentException("El esquema no puede estar vacio.", nameof(esquema));
        }

        if (!EsInicioIdentificadorValido(esquema[0]))
        {
            throw new ArgumentException($"El esquema '{esquema}' no es valido.", nameof(esquema));
        }

        for (int indice = 1; indice < esquema.Length; indice++)
        {
            if (!EsParteIdentificadorValida(esquema[indice]))
            {
                throw new ArgumentException($"El esquema '{esquema}' no es valido.", nameof(esquema));
            }
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
