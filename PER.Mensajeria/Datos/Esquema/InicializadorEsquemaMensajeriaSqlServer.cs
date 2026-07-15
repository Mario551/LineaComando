using Microsoft.Data.SqlClient;

namespace PER.Mensajeria.Datos.Esquema;

public class InicializadorEsquemaMensajeriaSqlServer
{
    private static readonly string[] TablasEsperadas =
    [
        "per_canales_comunicacion",
        "per_tipos_participante_conversacion",
        "per_tipos_mensaje",
        "per_direcciones_mensaje",
        "per_tipos_contenido_archivo",
        "per_tipos_procesamiento_interno_mensaje",
        "per_estados_procesamiento_interno_mensaje",
        "per_estados_envio_mensaje",
        "per_roles_contexto_ia",
        "per_tipos_entrada_contexto_ia",
        "per_cuentas_canal",
        "per_participantes_conversacion",
        "per_conversaciones",
        "per_conversaciones_participantes",
        "per_lineas_conversacion",
        "per_mensajes",
        "per_archivos_mensaje",
        "per_procesamientos_internos_mensaje",
        "per_metadata_razonamiento_ia_linea_conversacion",
        "per_entradas_contexto_ia",
        "per_estados_contexto_conversacion",
        "per_envios_mensaje"
    ];

    private readonly string connectionString;
    private readonly NombresBaseDatosMensajeria nombres;

    public InicializadorEsquemaMensajeriaSqlServer(string connectionString)
        : this(connectionString, "dbo")
    {
    }

    public InicializadorEsquemaMensajeriaSqlServer(string connectionString, string esquema)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        nombres = NombresBaseDatosMensajeria.SqlServer(esquema);
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        HashSet<string> tablasExistentes = await ObtenerTablasExistentesAsync(connection, cancellationToken);
        if (tablasExistentes.Count == TablasEsperadas.Length)
        {
            return;
        }

        if (tablasExistentes.Count > 0)
        {
            string faltantes = string.Join(", ", TablasEsperadas.Where(tabla => !tablasExistentes.Contains(tabla)));
            throw new InvalidOperationException(
                $"El esquema '{nombres.Esquema}' de mensajeria esta incompleto. Objetos faltantes: {faltantes}.");
        }

        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await CrearEsquemaAsync(connection, transaction, cancellationToken);
            await CrearTablasAsync(connection, transaction, cancellationToken);
            await CrearIndicesAsync(connection, transaction, cancellationToken);
            await InsertarCatalogosAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<HashSet<string>> ObtenerTablasExistentesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT tabla.name
            FROM sys.tables AS tabla
            INNER JOIN sys.schemas AS esquema ON esquema.schema_id = tabla.schema_id
            WHERE esquema.name = @esquema;
            """;

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@esquema", nombres.Esquema);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        HashSet<string> tablas = new(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            string tabla = reader.GetString(0);
            if (TablasEsperadas.Contains(tabla, StringComparer.OrdinalIgnoreCase))
            {
                tablas.Add(tabla);
            }
        }

        return tablas;
    }

    private async Task CrearEsquemaAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        string sql = $@"
IF SCHEMA_ID(N'{nombres.Esquema}') IS NULL
    EXEC(N'CREATE SCHEMA {nombres.EsquemaSql}');";

        await EjecutarAsync(connection, transaction, sql, cancellationToken);
    }

    private async Task CrearTablasAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        string sql = $@"
CREATE TABLE {nombres.CanalesComunicacion} (
    id INT IDENTITY(1,1) PRIMARY KEY,
    canal NVARCHAR(64) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL
);

CREATE TABLE {nombres.TiposParticipanteConversacion} (
    id NVARCHAR(32) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_tipos_participante_conversacion PRIMARY KEY (id)
);

CREATE TABLE {nombres.TiposMensaje} (
    id NVARCHAR(32) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_tipos_mensaje PRIMARY KEY (id)
);

CREATE TABLE {nombres.DireccionesMensaje} (
    id NVARCHAR(32) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_direcciones_mensaje PRIMARY KEY (id)
);

CREATE TABLE {nombres.TiposContenidoArchivo} (
    id NVARCHAR(128) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_tipos_contenido_archivo PRIMARY KEY (id)
);

CREATE TABLE {nombres.TiposProcesamientoInternoMensaje} (
    id NVARCHAR(128) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_tipos_procesamiento_interno_mensaje PRIMARY KEY (id)
);

CREATE TABLE {nombres.EstadosProcesamientoInternoMensaje} (
    id NVARCHAR(128) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_estados_procesamiento_interno_mensaje PRIMARY KEY (id)
);

CREATE TABLE {nombres.EstadosEnvioMensaje} (
    id NVARCHAR(32) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_estados_envio_mensaje PRIMARY KEY (id)
);

CREATE TABLE {nombres.RolesContextoIA} (
    id NVARCHAR(32) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_roles_contexto_ia PRIMARY KEY (id)
);

CREATE TABLE {nombres.TiposEntradaContextoIA} (
    id NVARCHAR(64) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    CONSTRAINT pk_per_tipos_entrada_contexto_ia PRIMARY KEY (id)
);

CREATE TABLE {nombres.CuentasCanal} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_canal_comunicacion INT NOT NULL,
    cuenta NVARCHAR(128) NOT NULL,
    descripcion NVARCHAR(MAX) NOT NULL,
    activa BIT NOT NULL,
    CONSTRAINT fk_cuentas_canal_canal_comunicacion FOREIGN KEY (id_canal_comunicacion) REFERENCES {nombres.CanalesComunicacion}(id) ON DELETE NO ACTION,
    CONSTRAINT uq_cuentas_canal_canal_cuenta UNIQUE (id_canal_comunicacion, cuenta)
);

CREATE TABLE {nombres.ParticipantesConversacion} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_tipo_participante_conversacion NVARCHAR(32) NOT NULL,
    identificador_participante NVARCHAR(256) NOT NULL,
    CONSTRAINT fk_participantes_conversacion_tipo FOREIGN KEY (id_tipo_participante_conversacion) REFERENCES {nombres.TiposParticipanteConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT uq_participantes_conversacion_tipo_identificador UNIQUE (id_tipo_participante_conversacion, identificador_participante)
);

CREATE TABLE {nombres.Conversaciones} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_cuenta_canal BIGINT NOT NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    fecha_actualizacion DATETIME2 NOT NULL,
    CONSTRAINT fk_conversaciones_cuenta_canal FOREIGN KEY (id_cuenta_canal) REFERENCES {nombres.CuentasCanal}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.ConversacionesParticipantes} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_participante_conversacion BIGINT NOT NULL,
    fecha_union DATETIME2 NOT NULL,
    fecha_salida DATETIME2 NULL,
    activo BIT NOT NULL,
    CONSTRAINT fk_conversaciones_participantes_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_conversaciones_participantes_participante FOREIGN KEY (id_participante_conversacion) REFERENCES {nombres.ParticipantesConversacion}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.LineasConversacion} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_estado_contexto_inicial BIGINT NULL,
    fecha_inicio DATETIME2 NOT NULL,
    fecha_ultima_actividad DATETIME2 NOT NULL,
    activa BIT NOT NULL,
    CONSTRAINT fk_lineas_conversacion_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.Mensajes} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_tipo_mensaje NVARCHAR(32) NOT NULL,
    id_direccion_mensaje NVARCHAR(32) NOT NULL,
    telefono_origen NVARCHAR(64) NULL,
    telefono_destino NVARCHAR(64) NULL,
    contenido NVARCHAR(MAX) NULL,
    identificador_externo_mensaje NVARCHAR(128) NULL,
    fecha_mensaje DATETIME2 NOT NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    fecha_actualizacion DATETIME2 NOT NULL,
    CONSTRAINT fk_mensajes_linea_conversacion FOREIGN KEY (id_linea_conversacion) REFERENCES {nombres.LineasConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_mensajes_tipo_mensaje FOREIGN KEY (id_tipo_mensaje) REFERENCES {nombres.TiposMensaje}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_mensajes_direccion_mensaje FOREIGN KEY (id_direccion_mensaje) REFERENCES {nombres.DireccionesMensaje}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.ArchivosMensaje} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_contenido_archivo NVARCHAR(128) NOT NULL,
    nombre_archivo NVARCHAR(MAX) NULL,
    tamano_bytes BIGINT NULL,
    ubicacion_archivo NVARCHAR(MAX) NOT NULL,
    proveedor_almacenamiento NVARCHAR(64) NOT NULL,
    identificador_externo_archivo NVARCHAR(256) NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_archivos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_archivos_mensaje_tipo_contenido FOREIGN KEY (id_tipo_contenido_archivo) REFERENCES {nombres.TiposContenidoArchivo}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.ProcesamientosInternosMensaje} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_procesamiento_interno_mensaje NVARCHAR(128) NOT NULL,
    id_estado_procesamiento_interno_mensaje NVARCHAR(128) NOT NULL,
    intentos INT NOT NULL,
    error NVARCHAR(MAX) NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    fecha_procesado DATETIME2 NULL,
    CONSTRAINT fk_procesamientos_internos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_procesamientos_internos_mensaje_tipo FOREIGN KEY (id_tipo_procesamiento_interno_mensaje) REFERENCES {nombres.TiposProcesamientoInternoMensaje}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_procesamientos_internos_mensaje_estado FOREIGN KEY (id_estado_procesamiento_interno_mensaje) REFERENCES {nombres.EstadosProcesamientoInternoMensaje}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.MetadataRazonamientoIALineaConversacion} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_procesamiento_interno_mensaje BIGINT NOT NULL,
    id_mensaje BIGINT NULL,
    proveedor NVARCHAR(128) NOT NULL,
    modelo NVARCHAR(256) NOT NULL,
    adaptador NVARCHAR(256) NOT NULL,
    iteracion INT NOT NULL,
    accion_decidida NVARCHAR(64) NOT NULL,
    finish_reason NVARCHAR(128) NULL,
    native_finish_reason NVARCHAR(128) NULL,
    prompt_tokens INT NULL,
    completion_tokens INT NULL,
    reasoning_tokens INT NULL,
    total_tokens INT NULL,
    request_json NVARCHAR(MAX) NULL,
    response_json NVARCHAR(MAX) NULL,
    content NVARCHAR(MAX) NULL,
    reasoning NVARCHAR(MAX) NULL,
    reasoning_details_json NVARCHAR(MAX) NULL,
    error NVARCHAR(MAX) NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_metadata_ia_linea FOREIGN KEY (id_linea_conversacion) REFERENCES {nombres.LineasConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_metadata_ia_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES {nombres.ProcesamientosInternosMensaje}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_metadata_ia_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.EstadosContextoConversacion} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_linea_conversacion_origen BIGINT NOT NULL,
    id_estado_contexto_anterior BIGINT NULL,
    id_metadata_razonamiento_ia BIGINT NOT NULL,
    version INT NOT NULL,
    contenido NVARCHAR(MAX) NOT NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_estados_contexto_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_estados_contexto_linea_origen FOREIGN KEY (id_linea_conversacion_origen) REFERENCES {nombres.LineasConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_estados_contexto_anterior FOREIGN KEY (id_estado_contexto_anterior) REFERENCES {nombres.EstadosContextoConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_estados_contexto_metadata FOREIGN KEY (id_metadata_razonamiento_ia) REFERENCES {nombres.MetadataRazonamientoIALineaConversacion}(id) ON DELETE NO ACTION
);

ALTER TABLE {nombres.LineasConversacion}
    ADD CONSTRAINT fk_lineas_conversacion_estado_contexto_inicial
    FOREIGN KEY (id_estado_contexto_inicial) REFERENCES {nombres.EstadosContextoConversacion}(id) ON DELETE NO ACTION;

CREATE TABLE {nombres.EntradasContextoIA} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_mensaje BIGINT NULL,
    id_procesamiento_interno_mensaje BIGINT NULL,
    id_metadata_razonamiento_ia BIGINT NULL,
    orden INT NOT NULL,
    id_rol_contexto_ia NVARCHAR(32) NOT NULL,
    id_tipo_entrada_contexto_ia NVARCHAR(64) NOT NULL,
    contenido NVARCHAR(MAX) NULL,
    tool_call_id NVARCHAR(128) NULL,
    fecha_entrada DATETIME2 NOT NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_entradas_contexto_ia_linea FOREIGN KEY (id_linea_conversacion) REFERENCES {nombres.LineasConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_entradas_contexto_ia_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_entradas_contexto_ia_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES {nombres.ProcesamientosInternosMensaje}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_entradas_contexto_ia_metadata FOREIGN KEY (id_metadata_razonamiento_ia) REFERENCES {nombres.MetadataRazonamientoIALineaConversacion}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_entradas_contexto_ia_rol FOREIGN KEY (id_rol_contexto_ia) REFERENCES {nombres.RolesContextoIA}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_entradas_contexto_ia_tipo FOREIGN KEY (id_tipo_entrada_contexto_ia) REFERENCES {nombres.TiposEntradaContextoIA}(id) ON DELETE NO ACTION
);

CREATE TABLE {nombres.EnviosMensaje} (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_estado_envio_mensaje NVARCHAR(32) NOT NULL,
    intentos INT NOT NULL,
    error NVARCHAR(MAX) NULL,
    fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
    fecha_ultimo_intento DATETIME2 NULL,
    fecha_enviado DATETIME2 NULL,
    CONSTRAINT fk_envios_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE NO ACTION,
    CONSTRAINT fk_envios_mensaje_estado FOREIGN KEY (id_estado_envio_mensaje) REFERENCES {nombres.EstadosEnvioMensaje}(id) ON DELETE NO ACTION
);";

        await EjecutarAsync(connection, transaction, sql, cancellationToken);
    }

    private async Task CrearIndicesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        string sql = $@"
CREATE INDEX ix_conversaciones_participantes_conversacion_activo ON {nombres.ConversacionesParticipantes} (id_conversacion, activo);
CREATE INDEX ix_lineas_conversacion_conversacion_activa_fecha ON {nombres.LineasConversacion} (id_conversacion, activa, fecha_ultima_actividad);
CREATE UNIQUE INDEX ux_lineas_conversacion_estado_contexto_inicial ON {nombres.LineasConversacion} (id_estado_contexto_inicial) WHERE id_estado_contexto_inicial IS NOT NULL;
CREATE INDEX ix_mensajes_linea_fecha_id ON {nombres.Mensajes} (id_linea_conversacion, fecha_creacion, id);
CREATE UNIQUE INDEX ux_mensajes_idempotencia ON {nombres.Mensajes} (id_linea_conversacion, id_direccion_mensaje, identificador_externo_mensaje) WHERE identificador_externo_mensaje IS NOT NULL;
CREATE INDEX ix_procesamientos_internos_mensaje_estado_fecha ON {nombres.ProcesamientosInternosMensaje} (id_estado_procesamiento_interno_mensaje, fecha_creacion);
CREATE INDEX ix_entradas_contexto_ia_linea_orden ON {nombres.EntradasContextoIA} (id_linea_conversacion, orden);
CREATE INDEX ix_entradas_contexto_ia_procesamiento_orden ON {nombres.EntradasContextoIA} (id_procesamiento_interno_mensaje, orden);
CREATE INDEX ix_metadata_ia_linea_iteracion ON {nombres.MetadataRazonamientoIALineaConversacion} (id_linea_conversacion, iteracion);
CREATE INDEX ix_metadata_ia_procesamiento_iteracion ON {nombres.MetadataRazonamientoIALineaConversacion} (id_procesamiento_interno_mensaje, iteracion);
CREATE UNIQUE INDEX ux_estados_contexto_linea_origen ON {nombres.EstadosContextoConversacion} (id_linea_conversacion_origen);
CREATE UNIQUE INDEX ux_estados_contexto_conversacion_version ON {nombres.EstadosContextoConversacion} (id_conversacion, version);
CREATE INDEX ix_estados_contexto_anterior ON {nombres.EstadosContextoConversacion} (id_estado_contexto_anterior);
CREATE UNIQUE INDEX ux_estados_contexto_metadata ON {nombres.EstadosContextoConversacion} (id_metadata_razonamiento_ia);
CREATE INDEX ix_envios_mensaje_estado_fecha ON {nombres.EnviosMensaje} (id_estado_envio_mensaje, fecha_creacion);";

        await EjecutarAsync(connection, transaction, sql, cancellationToken);
    }

    private async Task InsertarCatalogosAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        string sql = $@"
INSERT INTO {nombres.CanalesComunicacion} (canal, descripcion) VALUES
    ('whatsapp', 'WhatsApp'), ('web', 'Web'), ('api', 'API');

INSERT INTO {nombres.TiposParticipanteConversacion} (id, descripcion) VALUES
    ('telefono', 'Telefono'), ('usuario', 'Usuario'), ('sesion', 'Sesion'), ('api_cliente', 'Cliente API');

INSERT INTO {nombres.TiposMensaje} (id, descripcion) VALUES
    ('texto', 'Texto'), ('imagen', 'Imagen'), ('audio', 'Audio'), ('video', 'Video'), ('documento', 'Documento'), ('ubicacion', 'Ubicacion');

INSERT INTO {nombres.DireccionesMensaje} (id, descripcion) VALUES
    ('entrada', 'Entrada'), ('salida', 'Salida');

INSERT INTO {nombres.TiposContenidoArchivo} (id, descripcion) VALUES
    ('image/jpeg', 'Imagen JPEG'), ('image/png', 'Imagen PNG'), ('audio/ogg', 'Audio OGG'), ('audio/mpeg', 'Audio MPEG'), ('video/mp4', 'Video MP4'), ('application/pdf', 'Documento PDF');

INSERT INTO {nombres.TiposProcesamientoInternoMensaje} (id, descripcion) VALUES
    ('orquestar_entrada', 'Orquestar mensaje de entrada');

INSERT INTO {nombres.EstadosProcesamientoInternoMensaje} (id, descripcion) VALUES
    ('pendiente', 'Pendiente'), ('en_proceso', 'En proceso'), ('procesado', 'Procesado'), ('error', 'Error');

INSERT INTO {nombres.EstadosEnvioMensaje} (id, descripcion) VALUES
    ('pendiente', 'Pendiente'), ('enviado', 'Enviado'), ('entregado', 'Entregado'), ('leido', 'Leido'), ('fallido', 'Fallido');

INSERT INTO {nombres.RolesContextoIA} (id, descripcion) VALUES
    ('system', 'Sistema'), ('user', 'Usuario'), ('assistant', 'Asistente'), ('tool', 'Herramienta');

INSERT INTO {nombres.TiposEntradaContextoIA} (id, descripcion) VALUES
    ('mensaje_entrada', 'Mensaje de entrada'),
    ('decision_comando', 'Decision de comando'),
    ('decision_historial', 'Decision de historial'),
    ('respuesta_final', 'Respuesta final'),
    ('no_responder', 'No responder'),
    ('error_intencion', 'Error de intencion'),
    ('resultado_comando', 'Resultado de comando'),
    ('resultado_historial', 'Resultado de historial'),
    ('limite_ventana', 'Limite de ventana');";

        await EjecutarAsync(connection, transaction, sql, cancellationToken);
    }

    private static async Task EjecutarAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
