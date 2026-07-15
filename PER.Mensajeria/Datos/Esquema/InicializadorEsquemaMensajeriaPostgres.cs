using Npgsql;

namespace PER.Mensajeria.Datos.Esquema;

public class InicializadorEsquemaMensajeriaPostgres
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

    public InicializadorEsquemaMensajeriaPostgres(string connectionString)
        : this(connectionString, "public")
    {
    }

    public InicializadorEsquemaMensajeriaPostgres(string connectionString, string esquema)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        nombres = NombresBaseDatosMensajeria.Postgres(esquema);
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(connectionString);
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

        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await EjecutarAsync(
                connection,
                transaction,
                $"CREATE SCHEMA IF NOT EXISTS {nombres.EsquemaSql};",
                cancellationToken);
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
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = @esquema
              AND table_type = 'BASE TABLE';
            """;

        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("esquema", nombres.Esquema);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

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

    private async Task CrearTablasAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        string sql = $@"
CREATE TABLE {nombres.CanalesComunicacion} (
    id SERIAL PRIMARY KEY,
    canal VARCHAR(64) NOT NULL,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.TiposParticipanteConversacion} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.TiposMensaje} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.DireccionesMensaje} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.TiposContenidoArchivo} (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.TiposProcesamientoInternoMensaje} (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.EstadosProcesamientoInternoMensaje} (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.EstadosEnvioMensaje} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.RolesContextoIA} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.TiposEntradaContextoIA} (
    id VARCHAR(64) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE {nombres.CuentasCanal} (
    id BIGSERIAL PRIMARY KEY,
    id_canal_comunicacion INTEGER NOT NULL,
    cuenta VARCHAR(128) NOT NULL,
    descripcion TEXT NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_cuentas_canal_canal_comunicacion FOREIGN KEY (id_canal_comunicacion) REFERENCES {nombres.CanalesComunicacion}(id) ON DELETE RESTRICT,
    CONSTRAINT uq_cuentas_canal_canal_cuenta UNIQUE (id_canal_comunicacion, cuenta)
);

CREATE TABLE {nombres.ParticipantesConversacion} (
    id BIGSERIAL PRIMARY KEY,
    id_tipo_participante_conversacion VARCHAR(32) NOT NULL,
    identificador_participante VARCHAR(256) NOT NULL,
    CONSTRAINT fk_participantes_conversacion_tipo FOREIGN KEY (id_tipo_participante_conversacion) REFERENCES {nombres.TiposParticipanteConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT uq_participantes_conversacion_tipo_identificador UNIQUE (id_tipo_participante_conversacion, identificador_participante)
);

CREATE TABLE {nombres.Conversaciones} (
    id BIGSERIAL PRIMARY KEY,
    id_cuenta_canal BIGINT NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    CONSTRAINT fk_conversaciones_cuenta_canal FOREIGN KEY (id_cuenta_canal) REFERENCES {nombres.CuentasCanal}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.ConversacionesParticipantes} (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_participante_conversacion BIGINT NOT NULL,
    fecha_union TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_salida TIMESTAMP WITHOUT TIME ZONE NULL,
    activo BOOLEAN NOT NULL,
    CONSTRAINT fk_conversaciones_participantes_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_conversaciones_participantes_participante FOREIGN KEY (id_participante_conversacion) REFERENCES {nombres.ParticipantesConversacion}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.LineasConversacion} (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_estado_contexto_inicial BIGINT NULL,
    fecha_inicio TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_ultima_actividad TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_lineas_conversacion_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.Mensajes} (
    id BIGSERIAL PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_tipo_mensaje VARCHAR(32) NOT NULL,
    id_direccion_mensaje VARCHAR(32) NOT NULL,
    telefono_origen VARCHAR(64) NULL,
    telefono_destino VARCHAR(64) NULL,
    contenido TEXT NULL,
    identificador_externo_mensaje VARCHAR(128) NULL,
    fecha_mensaje TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    CONSTRAINT fk_mensajes_linea_conversacion FOREIGN KEY (id_linea_conversacion) REFERENCES {nombres.LineasConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mensajes_tipo_mensaje FOREIGN KEY (id_tipo_mensaje) REFERENCES {nombres.TiposMensaje}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mensajes_direccion_mensaje FOREIGN KEY (id_direccion_mensaje) REFERENCES {nombres.DireccionesMensaje}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.ArchivosMensaje} (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_contenido_archivo VARCHAR(128) NOT NULL,
    nombre_archivo TEXT NULL,
    tamano_bytes BIGINT NULL,
    ubicacion_archivo TEXT NOT NULL,
    proveedor_almacenamiento VARCHAR(64) NOT NULL,
    identificador_externo_archivo VARCHAR(256) NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_archivos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_archivos_mensaje_tipo_contenido FOREIGN KEY (id_tipo_contenido_archivo) REFERENCES {nombres.TiposContenidoArchivo}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.ProcesamientosInternosMensaje} (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_procesamiento_interno_mensaje VARCHAR(128) NOT NULL,
    id_estado_procesamiento_interno_mensaje VARCHAR(128) NOT NULL,
    intentos INTEGER NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_procesado TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_procesamientos_internos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_procesamientos_internos_mensaje_tipo FOREIGN KEY (id_tipo_procesamiento_interno_mensaje) REFERENCES {nombres.TiposProcesamientoInternoMensaje}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_procesamientos_internos_mensaje_estado FOREIGN KEY (id_estado_procesamiento_interno_mensaje) REFERENCES {nombres.EstadosProcesamientoInternoMensaje}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.MetadataRazonamientoIALineaConversacion} (
    id BIGSERIAL PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_procesamiento_interno_mensaje BIGINT NOT NULL,
    id_mensaje BIGINT NULL,
    proveedor VARCHAR(128) NOT NULL,
    modelo VARCHAR(256) NOT NULL,
    adaptador VARCHAR(256) NOT NULL,
    iteracion INTEGER NOT NULL,
    accion_decidida VARCHAR(64) NOT NULL,
    finish_reason VARCHAR(128) NULL,
    native_finish_reason VARCHAR(128) NULL,
    prompt_tokens INTEGER NULL,
    completion_tokens INTEGER NULL,
    reasoning_tokens INTEGER NULL,
    total_tokens INTEGER NULL,
    request_json TEXT NULL,
    response_json TEXT NULL,
    content TEXT NULL,
    reasoning TEXT NULL,
    reasoning_details_json TEXT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_metadata_ia_linea FOREIGN KEY (id_linea_conversacion) REFERENCES {nombres.LineasConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_ia_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES {nombres.ProcesamientosInternosMensaje}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_ia_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.EstadosContextoConversacion} (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_linea_conversacion_origen BIGINT NOT NULL,
    id_estado_contexto_anterior BIGINT NULL,
    id_metadata_razonamiento_ia BIGINT NOT NULL,
    version INTEGER NOT NULL,
    contenido TEXT NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_estados_contexto_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_estados_contexto_linea_origen FOREIGN KEY (id_linea_conversacion_origen) REFERENCES {nombres.LineasConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_estados_contexto_anterior FOREIGN KEY (id_estado_contexto_anterior) REFERENCES {nombres.EstadosContextoConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_estados_contexto_metadata FOREIGN KEY (id_metadata_razonamiento_ia) REFERENCES {nombres.MetadataRazonamientoIALineaConversacion}(id) ON DELETE RESTRICT
);

ALTER TABLE {nombres.LineasConversacion}
    ADD CONSTRAINT fk_lineas_conversacion_estado_contexto_inicial
    FOREIGN KEY (id_estado_contexto_inicial) REFERENCES {nombres.EstadosContextoConversacion}(id) ON DELETE RESTRICT;

CREATE TABLE {nombres.EntradasContextoIA} (
    id BIGSERIAL PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_mensaje BIGINT NULL,
    id_procesamiento_interno_mensaje BIGINT NULL,
    id_metadata_razonamiento_ia BIGINT NULL,
    orden INTEGER NOT NULL,
    id_rol_contexto_ia VARCHAR(32) NOT NULL,
    id_tipo_entrada_contexto_ia VARCHAR(64) NOT NULL,
    contenido TEXT NULL,
    tool_call_id VARCHAR(128) NULL,
    fecha_entrada TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_entradas_contexto_ia_linea FOREIGN KEY (id_linea_conversacion) REFERENCES {nombres.LineasConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_entradas_contexto_ia_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_entradas_contexto_ia_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES {nombres.ProcesamientosInternosMensaje}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_entradas_contexto_ia_metadata FOREIGN KEY (id_metadata_razonamiento_ia) REFERENCES {nombres.MetadataRazonamientoIALineaConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_entradas_contexto_ia_rol FOREIGN KEY (id_rol_contexto_ia) REFERENCES {nombres.RolesContextoIA}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_entradas_contexto_ia_tipo FOREIGN KEY (id_tipo_entrada_contexto_ia) REFERENCES {nombres.TiposEntradaContextoIA}(id) ON DELETE RESTRICT
);

CREATE TABLE {nombres.EnviosMensaje} (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_estado_envio_mensaje VARCHAR(32) NOT NULL,
    intentos INTEGER NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_ultimo_intento TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_enviado TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_envios_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_envios_mensaje_estado FOREIGN KEY (id_estado_envio_mensaje) REFERENCES {nombres.EstadosEnvioMensaje}(id) ON DELETE RESTRICT
);";

        await EjecutarAsync(connection, transaction, sql, cancellationToken);
    }

    private async Task CrearIndicesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
