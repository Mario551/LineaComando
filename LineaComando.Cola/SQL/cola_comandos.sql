-- Tabla: per_cola_comandos
-- Representa la cola de ejecución de comandos con relación a comandos registrados

CREATE TABLE IF NOT EXISTS per_cola_comandos (
    id BIGSERIAL PRIMARY KEY,
    id_comando_registrado INTEGER NOT NULL,
    ruta_comando VARCHAR(2048) NOT NULL,
    argumentos TEXT NULL,
    datos_comando JSONB NULL,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    fecha_leido TIMESTAMP NULL,
    fecha_ejecucion TIMESTAMP NULL,
    estado VARCHAR(50) NOT NULL DEFAULT 'Pendiente',
    mensaje_error TEXT NULL,
    salida TEXT NULL,
    duracion_ms BIGINT NULL,
    intentos INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT fk_cola_comandos_comando_registrado
        FOREIGN KEY (id_comando_registrado)
        REFERENCES per_comandos_registrados(id)
        ON DELETE NO ACTION
);

-- Índices para mejorar el rendimiento de consultas
CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_estado
    ON per_cola_comandos(estado);

CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_fecha_creacion
    ON per_cola_comandos(fecha_creacion);

CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_fecha_leido
    ON per_cola_comandos(fecha_leido)
    WHERE fecha_leido IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_pendientes
    ON per_cola_comandos(id, fecha_creacion)
    WHERE estado = 'Pendiente' AND fecha_leido IS NULL;

-- Función para obtener comandos pendientes (solo lectura)
CREATE OR REPLACE FUNCTION obtener_comandos_pendientes(
    p_tamanio_lote INTEGER DEFAULT 50,
    p_timeout_milisegundos INTEGER DEFAULT 300000
)
RETURNS TABLE (
    id BIGINT,
    id_comando_registrado INTEGER,
    ruta_comando VARCHAR(2048),
    argumentos TEXT,
    datos_comando JSONB,
    fecha_creacion TIMESTAMP,
    fecha_leido TIMESTAMP,
    fecha_ejecucion TIMESTAMP,
    estado VARCHAR(50),
    mensaje_error TEXT,
    salida TEXT,
    duracion_ms BIGINT,
    intentos INTEGER
)
AS $$
DECLARE
    v_timeout_timestamp TIMESTAMP;
BEGIN
    v_timeout_timestamp := NOW() - (p_timeout_milisegundos || ' milliseconds')::INTERVAL;

    RETURN QUERY
    SELECT c.*
    FROM per_cola_comandos c
    WHERE (
        (c.fecha_leido IS NULL AND c.estado = 'Pendiente')
        OR
        (c.estado = 'Procesando' AND c.fecha_leido < v_timeout_timestamp)
    )
    ORDER BY c.id
    LIMIT p_tamanio_lote;
END;
$$ LANGUAGE plpgsql;

-- Función para marcar comandos como procesando
CREATE OR REPLACE FUNCTION marcar_comandos_procesando(
    p_ids BIGINT[]
)
RETURNS TABLE (
    id BIGINT,
    id_comando_registrado INTEGER,
    ruta_comando VARCHAR(2048),
    argumentos TEXT,
    datos_comando JSONB,
    fecha_creacion TIMESTAMP,
    fecha_leido TIMESTAMP,
    fecha_ejecucion TIMESTAMP,
    estado VARCHAR(50),
    mensaje_error TEXT,
    salida TEXT,
    duracion_ms BIGINT,
    intentos INTEGER
)
AS $$
BEGIN
    UPDATE per_cola_comandos c
    SET fecha_leido = NOW(),
        estado = 'Procesando'
    WHERE c.id = ANY(p_ids);

    RETURN QUERY SELECT c.* FROM per_cola_comandos c WHERE c.id = ANY(p_ids);
END;
$$ LANGUAGE plpgsql;

-- Tabla: per_comandos_registrados
-- Almacena el catálogo de comandos disponibles en el sistema
CREATE TABLE IF NOT EXISTS per_comandos_registrados (
    id SERIAL PRIMARY KEY,
    ruta_comando VARCHAR(2048) NOT NULL UNIQUE,
    descripcion TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
    actualizado_en TIMESTAMP NULL
);

CREATE INDEX IF NOT EXISTS idx_per_comandos_registrados_ruta
    ON per_comandos_registrados(ruta_comando);

CREATE INDEX IF NOT EXISTS idx_per_comandos_registrados_activo
    ON per_comandos_registrados(activo);
