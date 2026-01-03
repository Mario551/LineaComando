-- ========================================
-- Sistema Event-Driven
-- Tablas para manejo de eventos y handlers
-- ========================================

-- Tabla: per_tipos_evento
-- Catálogo de tipos de eventos disponibles en el sistema
CREATE TABLE IF NOT EXISTS per_tipos_evento (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(255) NOT NULL UNIQUE,
    nombre VARCHAR(255) NOT NULL,
    descripcion TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_per_tipos_evento_codigo
    ON per_tipos_evento(codigo);

CREATE INDEX IF NOT EXISTS idx_per_tipos_evento_activo
    ON per_tipos_evento(activo);

-- Tabla: per_manejadores_evento
-- Catálogo de handlers disponibles
-- Los handlers son comandos que se ejecutan en respuesta a eventos o de forma programada
CREATE TABLE IF NOT EXISTS per_manejadores_evento (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(255) NOT NULL UNIQUE,
    nombre VARCHAR(255) NOT NULL,
    descripcion TEXT NULL,
    id_comando_registrado INTEGER NOT NULL,
    ruta_comando VARCHAR(2048) NOT NULL,
    argumentos_comando TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_manejador_comando
        FOREIGN KEY (id_comando_registrado)
        REFERENCES per_comandos_registrados(id)
        ON DELETE NO ACTION
);

CREATE INDEX IF NOT EXISTS idx_per_manejadores_evento_codigo
    ON per_manejadores_evento(codigo);

CREATE INDEX IF NOT EXISTS idx_per_manejadores_evento_activo
    ON per_manejadores_evento(activo);

CREATE INDEX IF NOT EXISTS idx_per_manejadores_evento_comando
    ON per_manejadores_evento(id_comando_registrado);

-- Tabla: per_disparadores_manejador
-- Configuración de ejecución de handlers
-- Define CUÁNDO y CÓMO se ejecuta cada handler
CREATE TABLE IF NOT EXISTS per_disparadores_manejador (
    id SERIAL PRIMARY KEY,
    manejador_evento_id INTEGER NOT NULL,
    modo_disparo VARCHAR(50) NOT NULL DEFAULT 'Evento',
    tipo_evento_id INTEGER NULL,
    expresion VARCHAR(255) NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    prioridad INTEGER NOT NULL DEFAULT 0,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_disparador_manejador
        FOREIGN KEY (manejador_evento_id)
        REFERENCES per_manejadores_evento(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_disparador_tipo_evento
        FOREIGN KEY (tipo_evento_id)
        REFERENCES per_tipos_evento(id)
        ON DELETE CASCADE,

    CONSTRAINT chk_modo_disparo
        CHECK (modo_disparo IN ('Evento', 'Programado')),

    CONSTRAINT chk_disparador_valido
        CHECK (
            (modo_disparo = 'Evento' AND tipo_evento_id IS NOT NULL) OR
            (modo_disparo = 'Programado' AND expresion IS NOT NULL)
        )
);

CREATE INDEX IF NOT EXISTS idx_per_disparadores_manejador_evento_id
    ON per_disparadores_manejador(manejador_evento_id);

CREATE INDEX IF NOT EXISTS idx_per_disparadores_tipo_evento
    ON per_disparadores_manejador(tipo_evento_id)
    WHERE tipo_evento_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_per_disparadores_modo
    ON per_disparadores_manejador(modo_disparo, activo);

CREATE INDEX IF NOT EXISTS idx_per_disparadores_programados
    ON per_disparadores_manejador(modo_disparo, activo, expresion)
    WHERE modo_disparo = 'Programado';

-- Tabla: per_eventos_outbox
-- Cola de eventos pendientes de procesar
CREATE TABLE IF NOT EXISTS per_eventos_outbox (
    id BIGSERIAL PRIMARY KEY,
    codigo_tipo_evento VARCHAR(255) NOT NULL,
    agregado_id BIGINT NULL,
    datos_evento JSONB NOT NULL,
    metadatos JSONB NULL,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
    procesado_en TIMESTAMP NULL
);

CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_tipo
    ON per_eventos_outbox(codigo_tipo_evento);

CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_procesado
    ON per_eventos_outbox(procesado_en)
    WHERE procesado_en IS NULL;

CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_creado
    ON per_eventos_outbox(creado_en);

CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_pendientes
    ON per_eventos_outbox(codigo_tipo_evento, creado_en)
    WHERE procesado_en IS NULL;

-- Función: obtener_eventos_pendientes
-- Obtiene eventos pendientes y los marca como en procesamiento
CREATE OR REPLACE FUNCTION obtener_eventos_pendientes(
    p_tamanio_lote INTEGER DEFAULT 50
)
RETURNS TABLE (
    id BIGINT,
    codigo_tipo_evento VARCHAR(255),
    agregado_id BIGINT,
    datos_evento JSONB,
    metadatos JSONB,
    creado_en TIMESTAMP,
    procesado_en TIMESTAMP
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        e.id,
        e.codigo_tipo_evento,
        e.agregado_id,
        e.datos_evento,
        e.metadatos,
        e.creado_en,
        e.procesado_en
    FROM per_eventos_outbox e
    WHERE e.procesado_en IS NULL
    ORDER BY e.creado_en
    LIMIT p_tamanio_lote;
END;
$$ LANGUAGE plpgsql;
