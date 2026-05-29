BEGIN;

CREATE TABLE per_canales_comunicacion (
    id SERIAL PRIMARY KEY,
    canal VARCHAR(64) NOT NULL,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_tipos_participante_conversacion (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_tipos_mensaje (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_direcciones_mensaje (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_tipos_contenido_archivo (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_tipos_procesamiento_interno_mensaje (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_estados_procesamiento_interno_mensaje (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_estados_envio_mensaje (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_cuentas_canal (
    id BIGSERIAL PRIMARY KEY,
    id_canal_comunicacion INTEGER NOT NULL,
    cuenta VARCHAR(128) NOT NULL,
    descripcion TEXT NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_cuentas_canal_canal_comunicacion FOREIGN KEY (id_canal_comunicacion) REFERENCES per_canales_comunicacion(id) ON DELETE RESTRICT,
    CONSTRAINT uq_cuentas_canal_canal_cuenta UNIQUE (id_canal_comunicacion, cuenta)
);

CREATE TABLE per_participantes_conversacion (
    id BIGSERIAL PRIMARY KEY,
    id_tipo_participante_conversacion VARCHAR(32) NOT NULL,
    identificador_participante VARCHAR(256) NOT NULL,
    CONSTRAINT fk_participantes_conversacion_tipo FOREIGN KEY (id_tipo_participante_conversacion) REFERENCES per_tipos_participante_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT uq_participantes_conversacion_tipo_identificador UNIQUE (id_tipo_participante_conversacion, identificador_participante)
);

CREATE TABLE per_conversaciones (
    id BIGSERIAL PRIMARY KEY,
    id_cuenta_canal BIGINT NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    CONSTRAINT fk_conversaciones_cuenta_canal FOREIGN KEY (id_cuenta_canal) REFERENCES per_cuentas_canal(id) ON DELETE RESTRICT
);

CREATE TABLE per_conversaciones_participantes (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_participante_conversacion BIGINT NOT NULL,
    fecha_union TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_salida TIMESTAMP WITHOUT TIME ZONE NULL,
    activo BOOLEAN NOT NULL,
    CONSTRAINT fk_conversaciones_participantes_conversacion FOREIGN KEY (id_conversacion) REFERENCES per_conversaciones(id) ON DELETE RESTRICT,
    CONSTRAINT fk_conversaciones_participantes_participante FOREIGN KEY (id_participante_conversacion) REFERENCES per_participantes_conversacion(id) ON DELETE RESTRICT
);

CREATE TABLE per_lineas_conversacion (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    fecha_inicio TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_ultima_actividad TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_lineas_conversacion_conversacion FOREIGN KEY (id_conversacion) REFERENCES per_conversaciones(id) ON DELETE RESTRICT
);

CREATE TABLE per_mensajes (
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
    CONSTRAINT fk_mensajes_linea_conversacion FOREIGN KEY (id_linea_conversacion) REFERENCES per_lineas_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mensajes_tipo_mensaje FOREIGN KEY (id_tipo_mensaje) REFERENCES per_tipos_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mensajes_direccion_mensaje FOREIGN KEY (id_direccion_mensaje) REFERENCES per_direcciones_mensaje(id) ON DELETE RESTRICT
);

CREATE TABLE per_archivos_mensaje (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_contenido_archivo VARCHAR(128) NOT NULL,
    nombre_archivo TEXT NULL,
    tamano_bytes BIGINT NULL,
    ubicacion_archivo TEXT NOT NULL,
    proveedor_almacenamiento VARCHAR(64) NOT NULL,
    identificador_externo_archivo VARCHAR(256) NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_archivos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES per_mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_archivos_mensaje_tipo_contenido FOREIGN KEY (id_tipo_contenido_archivo) REFERENCES per_tipos_contenido_archivo(id) ON DELETE RESTRICT
);

CREATE TABLE per_procesamientos_internos_mensaje (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_procesamiento_interno_mensaje VARCHAR(128) NOT NULL,
    id_estado_procesamiento_interno_mensaje VARCHAR(128) NOT NULL,
    intentos INTEGER NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_procesado TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_procesamientos_internos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES per_mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_procesamientos_internos_mensaje_tipo FOREIGN KEY (id_tipo_procesamiento_interno_mensaje) REFERENCES per_tipos_procesamiento_interno_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_procesamientos_internos_mensaje_estado FOREIGN KEY (id_estado_procesamiento_interno_mensaje) REFERENCES per_estados_procesamiento_interno_mensaje(id) ON DELETE RESTRICT
);

CREATE TABLE per_envios_mensaje (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_estado_envio_mensaje VARCHAR(32) NOT NULL,
    intentos INTEGER NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_ultimo_intento TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_enviado TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_envios_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES per_mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_envios_mensaje_estado FOREIGN KEY (id_estado_envio_mensaje) REFERENCES per_estados_envio_mensaje(id) ON DELETE RESTRICT
);

CREATE INDEX ix_conversaciones_participantes_conversacion_activo ON per_conversaciones_participantes (id_conversacion, activo);
CREATE INDEX ix_lineas_conversacion_conversacion_activa_fecha ON per_lineas_conversacion (id_conversacion, activa, fecha_ultima_actividad);
CREATE INDEX ix_mensajes_linea_fecha_id ON per_mensajes (id_linea_conversacion, fecha_creacion, id);
CREATE UNIQUE INDEX ux_mensajes_idempotencia ON per_mensajes (id_linea_conversacion, id_direccion_mensaje, identificador_externo_mensaje) WHERE identificador_externo_mensaje IS NOT NULL;
CREATE INDEX ix_procesamientos_internos_mensaje_estado_fecha ON per_procesamientos_internos_mensaje (id_estado_procesamiento_interno_mensaje, fecha_creacion);
CREATE INDEX ix_envios_mensaje_estado_fecha ON per_envios_mensaje (id_estado_envio_mensaje, fecha_creacion);

INSERT INTO per_canales_comunicacion (canal, descripcion) VALUES
    ('whatsapp', 'WhatsApp'),
    ('web', 'Web'),
    ('api', 'API');

INSERT INTO per_tipos_participante_conversacion (id, descripcion) VALUES
    ('telefono', 'Telefono'),
    ('usuario', 'Usuario'),
    ('sesion', 'Sesion'),
    ('api_cliente', 'Cliente API');

INSERT INTO per_tipos_mensaje (id, descripcion) VALUES
    ('texto', 'Texto'),
    ('imagen', 'Imagen'),
    ('audio', 'Audio'),
    ('video', 'Video'),
    ('documento', 'Documento'),
    ('ubicacion', 'Ubicacion');

INSERT INTO per_direcciones_mensaje (id, descripcion) VALUES
    ('entrada', 'Entrada'),
    ('salida', 'Salida');

INSERT INTO per_tipos_contenido_archivo (id, descripcion) VALUES
    ('image/jpeg', 'Imagen JPEG'),
    ('image/png', 'Imagen PNG'),
    ('audio/ogg', 'Audio OGG'),
    ('audio/mpeg', 'Audio MPEG'),
    ('video/mp4', 'Video MP4'),
    ('application/pdf', 'Documento PDF');

INSERT INTO per_tipos_procesamiento_interno_mensaje (id, descripcion) VALUES
    ('orquestar_entrada', 'Orquestar mensaje de entrada');

INSERT INTO per_estados_procesamiento_interno_mensaje (id, descripcion) VALUES
    ('pendiente', 'Pendiente'),
    ('en_proceso', 'En proceso'),
    ('procesado', 'Procesado'),
    ('error', 'Error');

INSERT INTO per_estados_envio_mensaje (id, descripcion) VALUES
    ('pendiente', 'Pendiente'),
    ('enviado', 'Enviado'),
    ('entregado', 'Entregado'),
    ('leido', 'Leido'),
    ('fallido', 'Fallido');

COMMIT;
