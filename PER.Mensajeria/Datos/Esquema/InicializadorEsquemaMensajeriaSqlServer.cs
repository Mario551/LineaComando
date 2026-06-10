using Microsoft.Data.SqlClient;

namespace PER.Mensajeria.Datos.Esquema;

public class InicializadorEsquemaMensajeriaSqlServer
{
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

        await CrearEsquemaAsync(connection, cancellationToken);
        await CrearTablasAsync(connection, cancellationToken);
        await CrearIndicesAsync(connection, cancellationToken);
        await InsertarCatalogosAsync(connection, cancellationToken);
    }

    private async Task CrearEsquemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
IF SCHEMA_ID(N'{nombres.Esquema}') IS NULL
    EXEC(N'CREATE SCHEMA {nombres.EsquemaSql}');";

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private async Task CrearTablasAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_canales_comunicacion' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.CanalesComunicacion} (
        id INT IDENTITY(1,1) PRIMARY KEY,
        canal NVARCHAR(64) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_tipos_participante_conversacion' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.TiposParticipanteConversacion} (
        id NVARCHAR(32) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_tipos_participante_conversacion PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_tipos_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.TiposMensaje} (
        id NVARCHAR(32) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_tipos_mensaje PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_direcciones_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.DireccionesMensaje} (
        id NVARCHAR(32) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_direcciones_mensaje PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_tipos_contenido_archivo' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.TiposContenidoArchivo} (
        id NVARCHAR(128) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_tipos_contenido_archivo PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_tipos_procesamiento_interno_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.TiposProcesamientoInternoMensaje} (
        id NVARCHAR(128) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_tipos_procesamiento_interno_mensaje PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_estados_procesamiento_interno_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.EstadosProcesamientoInternoMensaje} (
        id NVARCHAR(128) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_estados_procesamiento_interno_mensaje PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_estados_envio_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.EstadosEnvioMensaje} (
        id NVARCHAR(32) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        CONSTRAINT pk_per_estados_envio_mensaje PRIMARY KEY (id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_cuentas_canal' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.CuentasCanal} (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        id_canal_comunicacion INT NOT NULL,
        cuenta NVARCHAR(128) NOT NULL,
        descripcion NVARCHAR(MAX) NOT NULL,
        activa BIT NOT NULL,
        CONSTRAINT fk_cuentas_canal_canal_comunicacion FOREIGN KEY (id_canal_comunicacion) REFERENCES {nombres.CanalesComunicacion}(id) ON DELETE NO ACTION,
        CONSTRAINT uq_cuentas_canal_canal_cuenta UNIQUE (id_canal_comunicacion, cuenta)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_participantes_conversacion' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.ParticipantesConversacion} (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        id_tipo_participante_conversacion NVARCHAR(32) NOT NULL,
        identificador_participante NVARCHAR(256) NOT NULL,
        CONSTRAINT fk_participantes_conversacion_tipo FOREIGN KEY (id_tipo_participante_conversacion) REFERENCES {nombres.TiposParticipanteConversacion}(id) ON DELETE NO ACTION,
        CONSTRAINT uq_participantes_conversacion_tipo_identificador UNIQUE (id_tipo_participante_conversacion, identificador_participante)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_conversaciones' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.Conversaciones} (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        id_cuenta_canal BIGINT NOT NULL,
        fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        fecha_actualizacion DATETIME2 NOT NULL,
        CONSTRAINT fk_conversaciones_cuenta_canal FOREIGN KEY (id_cuenta_canal) REFERENCES {nombres.CuentasCanal}(id) ON DELETE NO ACTION
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_conversaciones_participantes' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
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
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_lineas_conversacion' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
    CREATE TABLE {nombres.LineasConversacion} (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        id_conversacion BIGINT NOT NULL,
        fecha_inicio DATETIME2 NOT NULL,
        fecha_ultima_actividad DATETIME2 NOT NULL,
        activa BIT NOT NULL,
        CONSTRAINT fk_lineas_conversacion_conversacion FOREIGN KEY (id_conversacion) REFERENCES {nombres.Conversaciones}(id) ON DELETE NO ACTION
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_mensajes' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
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
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_archivos_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
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
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_procesamientos_internos_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
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
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_envios_mensaje' AND schema_id = SCHEMA_ID(N'{nombres.Esquema}'))
BEGIN
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
    );
END";

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private async Task CrearIndicesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_conversaciones_participantes_conversacion_activo' AND object_id = OBJECT_ID(N'{nombres.Esquema}.per_conversaciones_participantes'))
    CREATE INDEX ix_conversaciones_participantes_conversacion_activo ON {nombres.ConversacionesParticipantes} (id_conversacion, activo);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_lineas_conversacion_conversacion_activa_fecha' AND object_id = OBJECT_ID(N'{nombres.Esquema}.per_lineas_conversacion'))
    CREATE INDEX ix_lineas_conversacion_conversacion_activa_fecha ON {nombres.LineasConversacion} (id_conversacion, activa, fecha_ultima_actividad);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_mensajes_linea_fecha_id' AND object_id = OBJECT_ID(N'{nombres.Esquema}.per_mensajes'))
    CREATE INDEX ix_mensajes_linea_fecha_id ON {nombres.Mensajes} (id_linea_conversacion, fecha_creacion, id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ux_mensajes_idempotencia' AND object_id = OBJECT_ID(N'{nombres.Esquema}.per_mensajes'))
    CREATE UNIQUE INDEX ux_mensajes_idempotencia ON {nombres.Mensajes} (id_linea_conversacion, id_direccion_mensaje, identificador_externo_mensaje) WHERE identificador_externo_mensaje IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_procesamientos_internos_mensaje_estado_fecha' AND object_id = OBJECT_ID(N'{nombres.Esquema}.per_procesamientos_internos_mensaje'))
    CREATE INDEX ix_procesamientos_internos_mensaje_estado_fecha ON {nombres.ProcesamientosInternosMensaje} (id_estado_procesamiento_interno_mensaje, fecha_creacion);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_envios_mensaje_estado_fecha' AND object_id = OBJECT_ID(N'{nombres.Esquema}.per_envios_mensaje'))
    CREATE INDEX ix_envios_mensaje_estado_fecha ON {nombres.EnviosMensaje} (id_estado_envio_mensaje, fecha_creacion);";

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private async Task InsertarCatalogosAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        string sql = $@"
MERGE {nombres.CanalesComunicacion} AS destino
USING (VALUES ('whatsapp', 'WhatsApp'), ('web', 'Web'), ('api', 'API')) AS origen (canal, descripcion)
ON destino.canal = origen.canal
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (canal, descripcion) VALUES (origen.canal, origen.descripcion);

MERGE {nombres.TiposParticipanteConversacion} AS destino
USING (VALUES ('telefono', 'Telefono'), ('usuario', 'Usuario'), ('sesion', 'Sesion'), ('api_cliente', 'Cliente API')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);

MERGE {nombres.TiposMensaje} AS destino
USING (VALUES ('texto', 'Texto'), ('imagen', 'Imagen'), ('audio', 'Audio'), ('video', 'Video'), ('documento', 'Documento'), ('ubicacion', 'Ubicacion')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);

MERGE {nombres.DireccionesMensaje} AS destino
USING (VALUES ('entrada', 'Entrada'), ('salida', 'Salida')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);

MERGE {nombres.TiposContenidoArchivo} AS destino
USING (VALUES ('image/jpeg', 'Imagen JPEG'), ('image/png', 'Imagen PNG'), ('audio/ogg', 'Audio OGG'), ('audio/mpeg', 'Audio MPEG'), ('video/mp4', 'Video MP4'), ('application/pdf', 'Documento PDF')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);

MERGE {nombres.TiposProcesamientoInternoMensaje} AS destino
USING (VALUES ('orquestar_entrada', 'Orquestar mensaje de entrada')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);

MERGE {nombres.EstadosProcesamientoInternoMensaje} AS destino
USING (VALUES ('pendiente', 'Pendiente'), ('en_proceso', 'En proceso'), ('procesado', 'Procesado'), ('error', 'Error')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);

MERGE {nombres.EstadosEnvioMensaje} AS destino
USING (VALUES ('pendiente', 'Pendiente'), ('enviado', 'Enviado'), ('entregado', 'Entregado'), ('leido', 'Leido'), ('fallido', 'Fallido')) AS origen (id, descripcion)
ON destino.id = origen.id
WHEN MATCHED THEN UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN INSERT (id, descripcion) VALUES (origen.id, origen.descripcion);";

        await EjecutarAsync(connection, sql, cancellationToken);
    }

    private static async Task EjecutarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
