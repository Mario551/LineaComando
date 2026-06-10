
using Npgsql;

namespace PER.Mensajeria.Datos.Esquema;

public class InicializadorEsquemaMensajeriaPostgres
{
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

        await EjecutarAsync(connection, $"CREATE SCHEMA IF NOT EXISTS {nombres.EsquemaSql};", cancellationToken);
        await CrearTablasAsync(connection, cancellationToken);
        await CrearIndicesAsync(connection, cancellationToken);
        await InsertarCatalogosAsync(connection, cancellationToken);
    }

    private async Task CrearTablasAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
CREATE TABLE IF NOT EXISTS {nombres.CanalesComunicacion} (
    id SERIAL PRIMARY KEY,
    canal VARCHAR(64) NOT NULL,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.TiposParticipanteConversacion} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.TiposMensaje} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.DireccionesMensaje} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.TiposContenidoArchivo} (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.TiposProcesamientoInternoMensaje} (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.EstadosProcesamientoInternoMensaje} (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.EstadosEnvioMensaje} (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {nombres.CuentasCanal} (
    id BIGSERIAL PRIMARY KEY,
    id_canal_comunicacion INTEGER NOT NULL,
    cuenta VARCHAR(128) NOT NULL,
    descripcion TEXT NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_cuentas_canal_canal_comunicacion FOREIGN KEY (id_canal_comunicacion) REFERENCES {nombres.CanalesComunicacion}(id) ON DELETE RESTRICT,
    CONSTRAINT uq_cuentas_canal_canal_cuenta UNIQUE (id_canal_comunicacion, cuenta)
);

CREATE TABLE IF NOT EXISTS {nombres.ParticipantesConversacion} (
    id BIGSERIAL PRIMARY KEY,
    id_tipo_participante_conversacion VARCHAR(32) NOT NULL,
    identificador_participante VARCHAR(256) NOT NULL,
    CONSTRAINT fk_participantes_conversacion_tipo FOREIGN KEY (id_tipo_participante_conversacion) REFERENCES {nombres.TiposParticipanteConversacion}(id) ON DELETE RESTRICT,
    CONSTRAINT uq_participantes_conversacion_tipo_identificador UNIQUE (id_tipo_participante_conversacion, identificador_participante)
);

CREATE TABLE IF NOT EXISTS {nombres.Conversaciones} (
    id BIGSERIAL PRIMARY KEY,
    id_cuenta_canal BIGINT NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    CONSTRAINT fk_conversaciones_cuenta_canal FOREIGN KEY (id_cuenta_canal) REFERENCES {nombres.CuentasCanal}(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS {nombres.ConversacionesParticipantes} (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_participante_conversacion BIGINT NOT NULL,
    fecha_union TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_salida TIMESTAMP WITHOUT TIME ZONE NULL,
    activo BOOLEAN NOT NULL,
    CONSTRAINT fk_conversaciones_participantes_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE RESTRICT,
    CONSTRAINT fk_conversaciones_participantes_participante FOREIGN KEY (id_participante_conversacion) REFERENCES {nombres.ParticipantesConversacion}(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS {nombres.LineasConversacion} (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    fecha_inicio TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_ultima_actividad TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_lineas_conversacion_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS {nombres.Mensajes} (
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

CREATE TABLE IF NOT EXISTS {nombres.ArchivosMensaje} (
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

CREATE TABLE IF NOT EXISTS {nombres.ProcesamientosInternosMensaje} (
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

CREATE TABLE IF NOT EXISTS {nombres.EnviosMensaje} (
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

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private async Task CrearIndicesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
CREATE INDEX IF NOT EXISTS ix_conversaciones_participantes_conversacion_activo ON {nombres.ConversacionesParticipantes} (id_conversacion, activo);
CREATE INDEX IF NOT EXISTS ix_lineas_conversacion_conversacion_activa_fecha ON {nombres.LineasConversacion} (id_conversacion, activa, fecha_ultima_actividad);
CREATE INDEX IF NOT EXISTS ix_mensajes_linea_fecha_id ON {nombres.Mensajes} (id_linea_conversacion, fecha_creacion, id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_mensajes_idempotencia ON {nombres.Mensajes} (id_linea_conversacion, id_direccion_mensaje, identificador_externo_mensaje) WHERE identificador_externo_mensaje IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_procesamientos_internos_mensaje_estado_fecha ON {nombres.ProcesamientosInternosMensaje} (id_estado_procesamiento_interno_mensaje, fecha_creacion);
CREATE INDEX IF NOT EXISTS ix_envios_mensaje_estado_fecha ON {nombres.EnviosMensaje} (id_estado_envio_mensaje, fecha_creacion);";

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private async Task InsertarCatalogosAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
INSERT INTO {nombres.CanalesComunicacion} (canal, descripcion)
SELECT 'whatsapp', 'WhatsApp' WHERE NOT EXISTS (SELECT 1 FROM {nombres.CanalesComunicacion} WHERE canal = 'whatsapp');
INSERT INTO {nombres.CanalesComunicacion} (canal, descripcion)
SELECT 'web', 'Web' WHERE NOT EXISTS (SELECT 1 FROM {nombres.CanalesComunicacion} WHERE canal = 'web');
INSERT INTO {nombres.CanalesComunicacion} (canal, descripcion)
SELECT 'api', 'API' WHERE NOT EXISTS (SELECT 1 FROM {nombres.CanalesComunicacion} WHERE canal = 'api');

INSERT INTO {nombres.TiposParticipanteConversacion} (id, descripcion) VALUES
    ('telefono', 'Telefono'), ('usuario', 'Usuario'), ('sesion', 'Sesion'), ('api_cliente', 'Cliente API')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;

INSERT INTO {nombres.TiposMensaje} (id, descripcion) VALUES
    ('texto', 'Texto'), ('imagen', 'Imagen'), ('audio', 'Audio'), ('video', 'Video'), ('documento', 'Documento'), ('ubicacion', 'Ubicacion')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;

INSERT INTO {nombres.DireccionesMensaje} (id, descripcion) VALUES
    ('entrada', 'Entrada'), ('salida', 'Salida')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;

INSERT INTO {nombres.TiposContenidoArchivo} (id, descripcion) VALUES
    ('image/jpeg', 'Imagen JPEG'), ('image/png', 'Imagen PNG'), ('audio/ogg', 'Audio OGG'), ('audio/mpeg', 'Audio MPEG'), ('video/mp4', 'Video MP4'), ('application/pdf', 'Documento PDF')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;

INSERT INTO {nombres.TiposProcesamientoInternoMensaje} (id, descripcion) VALUES
    ('orquestar_entrada', 'Orquestar mensaje de entrada')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;

INSERT INTO {nombres.EstadosProcesamientoInternoMensaje} (id, descripcion) VALUES
    ('pendiente', 'Pendiente'), ('en_proceso', 'En proceso'), ('procesado', 'Procesado'), ('error', 'Error')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;

INSERT INTO {nombres.EstadosEnvioMensaje} (id, descripcion) VALUES
    ('pendiente', 'Pendiente'), ('enviado', 'Enviado'), ('entregado', 'Entregado'), ('leido', 'Leido'), ('fallido', 'Fallido')
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion;";

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private static async Task EjecutarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
