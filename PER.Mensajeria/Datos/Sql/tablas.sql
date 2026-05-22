BEGIN;

CREATE TABLE canales_comunicacion (
    id SERIAL PRIMARY KEY,
    canal VARCHAR(64) NOT NULL,
    descripcion TEXT NOT NULL
);

CREATE TABLE tipos_participante_conversacion (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE tipos_mensaje (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE direcciones_mensaje (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE tipos_contenido_archivo (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE tipos_procesamiento_interno_mensaje (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE estados_procesamiento_interno_mensaje (
    id VARCHAR(128) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE estados_envio_mensaje (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE cuentas_canal (
    id BIGSERIAL PRIMARY KEY,
    id_canal_comunicacion INTEGER NOT NULL,
    cuenta VARCHAR(128) NOT NULL,
    descripcion TEXT NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_cuentas_canal_canal_comunicacion FOREIGN KEY (id_canal_comunicacion) REFERENCES canales_comunicacion(id) ON DELETE RESTRICT,
    CONSTRAINT uq_cuentas_canal_canal_cuenta UNIQUE (id_canal_comunicacion, cuenta)
);

CREATE TABLE participantes_conversacion (
    id BIGSERIAL PRIMARY KEY,
    id_tipo_participante_conversacion VARCHAR(32) NOT NULL,
    identificador_participante VARCHAR(256) NOT NULL,
    CONSTRAINT fk_participantes_conversacion_tipo FOREIGN KEY (id_tipo_participante_conversacion) REFERENCES tipos_participante_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT uq_participantes_conversacion_tipo_identificador UNIQUE (id_tipo_participante_conversacion, identificador_participante)
);

CREATE TABLE conversaciones (
    id BIGSERIAL PRIMARY KEY,
    id_cuenta_canal BIGINT NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    CONSTRAINT fk_conversaciones_cuenta_canal FOREIGN KEY (id_cuenta_canal) REFERENCES cuentas_canal(id) ON DELETE RESTRICT
);

CREATE TABLE conversaciones_participantes (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_participante_conversacion BIGINT NOT NULL,
    fecha_union TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_salida TIMESTAMP WITHOUT TIME ZONE NULL,
    activo BOOLEAN NOT NULL,
    CONSTRAINT fk_conversaciones_participantes_conversacion FOREIGN KEY (id_conversacion) REFERENCES conversaciones(id) ON DELETE RESTRICT,
    CONSTRAINT fk_conversaciones_participantes_participante FOREIGN KEY (id_participante_conversacion) REFERENCES participantes_conversacion(id) ON DELETE RESTRICT
);

CREATE TABLE lineas_conversacion (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    fecha_inicio TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_ultima_actividad TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    activa BOOLEAN NOT NULL,
    CONSTRAINT fk_lineas_conversacion_conversacion FOREIGN KEY (id_conversacion) REFERENCES conversaciones(id) ON DELETE RESTRICT
);

CREATE TABLE mensajes (
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
    CONSTRAINT fk_mensajes_linea_conversacion FOREIGN KEY (id_linea_conversacion) REFERENCES lineas_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mensajes_tipo_mensaje FOREIGN KEY (id_tipo_mensaje) REFERENCES tipos_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mensajes_direccion_mensaje FOREIGN KEY (id_direccion_mensaje) REFERENCES direcciones_mensaje(id) ON DELETE RESTRICT
);

CREATE TABLE archivos_mensaje (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_contenido_archivo VARCHAR(128) NOT NULL,
    nombre_archivo TEXT NULL,
    tamano_bytes BIGINT NULL,
    ubicacion_archivo TEXT NOT NULL,
    proveedor_almacenamiento VARCHAR(64) NOT NULL,
    identificador_externo_archivo VARCHAR(256) NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_archivos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_archivos_mensaje_tipo_contenido FOREIGN KEY (id_tipo_contenido_archivo) REFERENCES tipos_contenido_archivo(id) ON DELETE RESTRICT
);

CREATE TABLE procesamientos_internos_mensaje (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_tipo_procesamiento_interno_mensaje VARCHAR(128) NOT NULL,
    id_estado_procesamiento_interno_mensaje VARCHAR(128) NOT NULL,
    intentos INTEGER NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_procesado TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_procesamientos_internos_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_procesamientos_internos_mensaje_tipo FOREIGN KEY (id_tipo_procesamiento_interno_mensaje) REFERENCES tipos_procesamiento_interno_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_procesamientos_internos_mensaje_estado FOREIGN KEY (id_estado_procesamiento_interno_mensaje) REFERENCES estados_procesamiento_interno_mensaje(id) ON DELETE RESTRICT
);

CREATE TABLE envios_mensaje (
    id BIGSERIAL PRIMARY KEY,
    id_mensaje BIGINT NOT NULL,
    id_estado_envio_mensaje VARCHAR(32) NOT NULL,
    intentos INTEGER NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_ultimo_intento TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_enviado TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_envios_mensaje_mensaje FOREIGN KEY (id_mensaje) REFERENCES mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_envios_mensaje_estado FOREIGN KEY (id_estado_envio_mensaje) REFERENCES estados_envio_mensaje(id) ON DELETE RESTRICT
);

CREATE INDEX ix_conversaciones_participantes_conversacion_activo ON conversaciones_participantes (id_conversacion, activo);
CREATE INDEX ix_lineas_conversacion_conversacion_activa_fecha ON lineas_conversacion (id_conversacion, activa, fecha_ultima_actividad);
CREATE INDEX ix_mensajes_linea_fecha_id ON mensajes (id_linea_conversacion, fecha_creacion, id);
CREATE UNIQUE INDEX ux_mensajes_idempotencia ON mensajes (id_linea_conversacion, id_direccion_mensaje, identificador_externo_mensaje) WHERE identificador_externo_mensaje IS NOT NULL;
CREATE INDEX ix_procesamientos_internos_mensaje_estado_fecha ON procesamientos_internos_mensaje (id_estado_procesamiento_interno_mensaje, fecha_creacion);
CREATE INDEX ix_envios_mensaje_estado_fecha ON envios_mensaje (id_estado_envio_mensaje, fecha_creacion);

INSERT INTO canales_comunicacion (canal, descripcion) VALUES
    ('whatsapp', 'WhatsApp'),
    ('web', 'Web'),
    ('api', 'API');

INSERT INTO tipos_participante_conversacion (id, descripcion) VALUES
    ('telefono', 'Telefono'),
    ('usuario', 'Usuario'),
    ('sesion', 'Sesion'),
    ('api_cliente', 'Cliente API');

INSERT INTO tipos_mensaje (id, descripcion) VALUES
    ('texto', 'Texto'),
    ('imagen', 'Imagen'),
    ('audio', 'Audio'),
    ('video', 'Video'),
    ('documento', 'Documento'),
    ('ubicacion', 'Ubicacion');

INSERT INTO direcciones_mensaje (id, descripcion) VALUES
    ('entrada', 'Entrada'),
    ('salida', 'Salida');

INSERT INTO tipos_contenido_archivo (id, descripcion) VALUES
    ('image/jpeg', 'Imagen JPEG'),
    ('image/png', 'Imagen PNG'),
    ('audio/ogg', 'Audio OGG'),
    ('audio/mpeg', 'Audio MPEG'),
    ('video/mp4', 'Video MP4'),
    ('application/pdf', 'Documento PDF');

INSERT INTO tipos_procesamiento_interno_mensaje (id, descripcion) VALUES
    ('orquestar_entrada', 'Orquestar mensaje de entrada');

INSERT INTO estados_procesamiento_interno_mensaje (id, descripcion) VALUES
    ('pendiente', 'Pendiente'),
    ('en_proceso', 'En proceso'),
    ('procesado', 'Procesado'),
    ('error', 'Error');

INSERT INTO estados_envio_mensaje (id, descripcion) VALUES
    ('pendiente', 'Pendiente'),
    ('enviado', 'Enviado'),
    ('entregado', 'Entregado'),
    ('leido', 'Leido'),
    ('fallido', 'Fallido');

COMMIT;
