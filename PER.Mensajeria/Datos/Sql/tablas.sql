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

CREATE TABLE per_roles_contexto_ia (
    id VARCHAR(32) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_tipos_entrada_contexto_ia (
    id VARCHAR(64) PRIMARY KEY,
    descripcion TEXT NOT NULL
);

CREATE TABLE per_estados_ejecucion_comando_contexto (
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
    id_compactacion_contexto_inicial BIGINT NULL,
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

CREATE TABLE per_informacion_tecnica_llamadas_ia_linea_conversacion (
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
    CONSTRAINT fk_informacion_tecnica_ia_linea FOREIGN KEY (id_linea_conversacion) REFERENCES per_lineas_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_informacion_tecnica_ia_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES per_procesamientos_internos_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_informacion_tecnica_ia_mensaje FOREIGN KEY (id_mensaje) REFERENCES per_mensajes(id) ON DELETE RESTRICT
);

CREATE TABLE per_compactaciones_contexto_conversacion (
    id BIGSERIAL PRIMARY KEY,
    id_conversacion BIGINT NOT NULL,
    id_linea_conversacion_origen BIGINT NOT NULL,
    id_compactacion_contexto_anterior BIGINT NULL,
    id_informacion_tecnica_llamada_ia BIGINT NOT NULL,
    version INTEGER NOT NULL,
    contenido TEXT NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_compactaciones_contexto_conversacion FOREIGN KEY (id_conversacion) REFERENCES per_conversaciones(id) ON DELETE RESTRICT,
    CONSTRAINT fk_compactaciones_contexto_linea_origen FOREIGN KEY (id_linea_conversacion_origen) REFERENCES per_lineas_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_compactaciones_contexto_anterior FOREIGN KEY (id_compactacion_contexto_anterior) REFERENCES per_compactaciones_contexto_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_compactaciones_contexto_informacion_ia FOREIGN KEY (id_informacion_tecnica_llamada_ia) REFERENCES per_informacion_tecnica_llamadas_ia_linea_conversacion(id) ON DELETE RESTRICT
);

ALTER TABLE per_lineas_conversacion
    ADD CONSTRAINT fk_lineas_conversacion_compactacion_contexto_inicial
    FOREIGN KEY (id_compactacion_contexto_inicial) REFERENCES per_compactaciones_contexto_conversacion(id) ON DELETE RESTRICT;

CREATE TABLE per_metadata_entradas_contexto_ia (
    id BIGSERIAL PRIMARY KEY,
    id_linea_conversacion BIGINT NOT NULL,
    id_mensaje BIGINT NULL,
    id_procesamiento_interno_mensaje BIGINT NULL,
    id_informacion_tecnica_llamada_ia BIGINT NULL,
    id_compactacion_contexto_incorporada BIGINT NULL,
    orden INTEGER NOT NULL,
    id_rol_contexto_ia VARCHAR(32) NOT NULL,
    id_tipo_entrada_contexto_ia VARCHAR(64) NOT NULL,
    contenido TEXT NULL,
    tool_call_id VARCHAR(128) NULL,
    fecha_entrada TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    CONSTRAINT fk_metadata_entradas_contexto_ia_linea FOREIGN KEY (id_linea_conversacion) REFERENCES per_lineas_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_entradas_contexto_ia_mensaje FOREIGN KEY (id_mensaje) REFERENCES per_mensajes(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_entradas_contexto_ia_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES per_procesamientos_internos_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_entradas_contexto_ia_informacion FOREIGN KEY (id_informacion_tecnica_llamada_ia) REFERENCES per_informacion_tecnica_llamadas_ia_linea_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_entradas_contexto_ia_compactacion FOREIGN KEY (id_compactacion_contexto_incorporada) REFERENCES per_compactaciones_contexto_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_entradas_contexto_ia_rol FOREIGN KEY (id_rol_contexto_ia) REFERENCES per_roles_contexto_ia(id) ON DELETE RESTRICT,
    CONSTRAINT fk_metadata_entradas_contexto_ia_tipo FOREIGN KEY (id_tipo_entrada_contexto_ia) REFERENCES per_tipos_entrada_contexto_ia(id) ON DELETE RESTRICT
);

CREATE TABLE per_ejecuciones_comando_contexto (
    id BIGSERIAL PRIMARY KEY,
    id_ejecucion_anterior BIGINT NULL,
    id_linea_conversacion BIGINT NOT NULL,
    id_procesamiento_interno_mensaje BIGINT NOT NULL,
    id_metadata_entrada_decision_contexto_ia BIGINT NOT NULL,
    id_metadata_entrada_resultado_contexto_ia BIGINT NULL,
    numero_intento INTEGER NOT NULL,
    proveedor_ejecucion VARCHAR(64) NOT NULL,
    identificador_externo VARCHAR(128) NULL,
    codigo_comando VARCHAR(256) NOT NULL,
    parametros_json TEXT NOT NULL,
    id_estado_ejecucion_comando_contexto VARCHAR(32) NOT NULL,
    activa BOOLEAN NOT NULL,
    error TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT LOCALTIMESTAMP,
    fecha_inicio_encolado TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_encolado TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_finalizacion TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT fk_ejecuciones_comando_contexto_anterior FOREIGN KEY (id_ejecucion_anterior) REFERENCES per_ejecuciones_comando_contexto(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ejecuciones_comando_contexto_linea FOREIGN KEY (id_linea_conversacion) REFERENCES per_lineas_conversacion(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ejecuciones_comando_contexto_procesamiento FOREIGN KEY (id_procesamiento_interno_mensaje) REFERENCES per_procesamientos_internos_mensaje(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ejecuciones_comando_contexto_decision FOREIGN KEY (id_metadata_entrada_decision_contexto_ia) REFERENCES per_metadata_entradas_contexto_ia(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ejecuciones_comando_contexto_resultado FOREIGN KEY (id_metadata_entrada_resultado_contexto_ia) REFERENCES per_metadata_entradas_contexto_ia(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ejecuciones_comando_contexto_estado FOREIGN KEY (id_estado_ejecucion_comando_contexto) REFERENCES per_estados_ejecucion_comando_contexto(id) ON DELETE RESTRICT
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
CREATE UNIQUE INDEX ux_lineas_conversacion_compactacion_contexto_inicial ON per_lineas_conversacion (id_compactacion_contexto_inicial) WHERE id_compactacion_contexto_inicial IS NOT NULL;
CREATE INDEX ix_mensajes_linea_fecha_id ON per_mensajes (id_linea_conversacion, fecha_creacion, id);
CREATE UNIQUE INDEX ux_mensajes_idempotencia ON per_mensajes (id_linea_conversacion, id_direccion_mensaje, identificador_externo_mensaje) WHERE identificador_externo_mensaje IS NOT NULL;
CREATE INDEX ix_procesamientos_internos_mensaje_estado_fecha ON per_procesamientos_internos_mensaje (id_estado_procesamiento_interno_mensaje, fecha_creacion);
CREATE INDEX ix_metadata_entradas_ia_linea_orden ON per_metadata_entradas_contexto_ia (id_linea_conversacion, orden);
CREATE INDEX ix_metadata_entradas_ia_procesamiento_orden ON per_metadata_entradas_contexto_ia (id_procesamiento_interno_mensaje, orden);
CREATE INDEX ix_metadata_entradas_ia_compactacion ON per_metadata_entradas_contexto_ia (id_compactacion_contexto_incorporada);
CREATE INDEX ix_informacion_tecnica_ia_linea_iteracion ON per_informacion_tecnica_llamadas_ia_linea_conversacion (id_linea_conversacion, iteracion);
CREATE INDEX ix_informacion_tecnica_ia_procesamiento_iteracion ON per_informacion_tecnica_llamadas_ia_linea_conversacion (id_procesamiento_interno_mensaje, iteracion);
CREATE UNIQUE INDEX ux_ejecuciones_comando_contexto_activa_procesamiento ON per_ejecuciones_comando_contexto (id_procesamiento_interno_mensaje) WHERE activa = TRUE;
CREATE UNIQUE INDEX ux_ejecuciones_comando_contexto_decision_intento ON per_ejecuciones_comando_contexto (id_metadata_entrada_decision_contexto_ia, numero_intento);
CREATE UNIQUE INDEX ux_ejecuciones_comando_contexto_externa ON per_ejecuciones_comando_contexto (proveedor_ejecucion, identificador_externo) WHERE identificador_externo IS NOT NULL;
CREATE UNIQUE INDEX ux_ejecuciones_comando_contexto_anterior ON per_ejecuciones_comando_contexto (id_ejecucion_anterior) WHERE id_ejecucion_anterior IS NOT NULL;
CREATE UNIQUE INDEX ux_ejecuciones_comando_contexto_resultado ON per_ejecuciones_comando_contexto (id_metadata_entrada_resultado_contexto_ia) WHERE id_metadata_entrada_resultado_contexto_ia IS NOT NULL;
CREATE UNIQUE INDEX ux_compactaciones_contexto_linea_origen ON per_compactaciones_contexto_conversacion (id_linea_conversacion_origen);
CREATE UNIQUE INDEX ux_compactaciones_contexto_conversacion_version ON per_compactaciones_contexto_conversacion (id_conversacion, version);
CREATE INDEX ix_compactaciones_contexto_anterior ON per_compactaciones_contexto_conversacion (id_compactacion_contexto_anterior);
CREATE UNIQUE INDEX ux_compactaciones_contexto_informacion_ia ON per_compactaciones_contexto_conversacion (id_informacion_tecnica_llamada_ia);
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

INSERT INTO per_roles_contexto_ia (id, descripcion) VALUES
    ('system', 'Sistema'),
    ('user', 'Usuario'),
    ('assistant', 'Asistente'),
    ('tool', 'Herramienta');

INSERT INTO per_tipos_entrada_contexto_ia (id, descripcion) VALUES
    ('mensaje_entrada', 'Mensaje de entrada'),
    ('decision_comando', 'Decision de comando'),
    ('decision_consulta_mensajes_linea_anterior', 'Decision de consulta de mensajes de linea anterior'),
    ('respuesta_final', 'Respuesta final'),
    ('no_responder', 'No responder'),
    ('error_intencion', 'Error de intencion'),
    ('resultado_comando', 'Resultado de comando'),
    ('resultado_consulta_mensajes_linea_anterior', 'Resultado de consulta de mensajes de linea anterior'),
    ('limite_ventana', 'Limite de ventana');

INSERT INTO per_estados_ejecucion_comando_contexto (id, descripcion) VALUES
    ('preparada', 'Preparada para encolar'),
    ('encolando', 'Solicitud de encolado iniciada'),
    ('encolada', 'Encolada en el proveedor externo'),
    ('abandonando', 'Abandono del intento externo en curso'),
    ('completada', 'Ejecucion completada'),
    ('fallida', 'Ejecucion terminada con error'),
    ('abandonada', 'Ejecucion reemplazada por un reintento'),
    ('incierta', 'No se pudo determinar la ejecucion externa');

COMMIT;
