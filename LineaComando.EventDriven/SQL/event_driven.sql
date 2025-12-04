-- ========================================
-- Sistema Event-Driven
-- Tablas para manejo de eventos y handlers
-- ========================================

-- Tabla: tipos_evento
-- Catálogo de tipos de eventos disponibles en el sistema
CREATE TABLE IF NOT EXISTS tipos_evento (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(255) NOT NULL UNIQUE,
    nombre VARCHAR(255) NOT NULL,
    descripcion TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tipos_evento_codigo
    ON tipos_evento(codigo);

CREATE INDEX IF NOT EXISTS idx_tipos_evento_activo
    ON tipos_evento(activo);

-- Tabla: manejadores_evento
-- Catálogo de handlers disponibles
-- Los handlers son comandos que se ejecutan en respuesta a eventos o de forma programada
CREATE TABLE IF NOT EXISTS manejadores_evento (
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
        REFERENCES comandos_registrados(id)
        ON DELETE NO ACTION
);

CREATE INDEX IF NOT EXISTS idx_manejadores_evento_codigo
    ON manejadores_evento(codigo);

CREATE INDEX IF NOT EXISTS idx_manejadores_evento_activo
    ON manejadores_evento(activo);

CREATE INDEX IF NOT EXISTS idx_manejadores_evento_comando
    ON manejadores_evento(id_comando_registrado);

-- Tabla: disparadores_manejador
-- Configuración de ejecución de handlers
-- Define CUÁNDO y CÓMO se ejecuta cada handler
CREATE TABLE IF NOT EXISTS disparadores_manejador (
    id SERIAL PRIMARY KEY,
    manejador_evento_id INTEGER NOT NULL,
    modo_disparo VARCHAR(50) NOT NULL DEFAULT 'Evento',
    tipo_evento_id INTEGER NULL,
    expresion_cron VARCHAR(255) NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    prioridad INTEGER NOT NULL DEFAULT 0,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_disparador_manejador
        FOREIGN KEY (manejador_evento_id)
        REFERENCES manejadores_evento(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_disparador_tipo_evento
        FOREIGN KEY (tipo_evento_id)
        REFERENCES tipos_evento(id)
        ON DELETE CASCADE,

    CONSTRAINT chk_modo_disparo
        CHECK (modo_disparo IN ('Evento', 'Programado')),

    CONSTRAINT chk_disparador_valido
        CHECK (
            (modo_disparo = 'Evento' AND tipo_evento_id IS NOT NULL) OR
            (modo_disparo = 'Programado' AND expresion_cron IS NOT NULL)
        )
);

CREATE INDEX IF NOT EXISTS idx_disparadores_manejador_evento_id
    ON disparadores_manejador(manejador_evento_id);

CREATE INDEX IF NOT EXISTS idx_disparadores_tipo_evento
    ON disparadores_manejador(tipo_evento_id)
    WHERE tipo_evento_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_disparadores_modo
    ON disparadores_manejador(modo_disparo, activo);

CREATE INDEX IF NOT EXISTS idx_disparadores_programados
    ON disparadores_manejador(modo_disparo, activo, expresion_cron)
    WHERE modo_disparo = 'Programado';

-- Tabla: eventos_outbox
-- Cola de eventos pendientes de procesar
CREATE TABLE IF NOT EXISTS eventos_outbox (
    id BIGSERIAL PRIMARY KEY,
    codigo_tipo_evento VARCHAR(255) NOT NULL,
    agregado_id BIGINT NULL,
    datos_evento JSONB NOT NULL,
    metadatos JSONB NULL,
    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
    procesado_en TIMESTAMP NULL
);

CREATE INDEX IF NOT EXISTS idx_eventos_outbox_tipo
    ON eventos_outbox(codigo_tipo_evento);

CREATE INDEX IF NOT EXISTS idx_eventos_outbox_procesado
    ON eventos_outbox(procesado_en)
    WHERE procesado_en IS NULL;

CREATE INDEX IF NOT EXISTS idx_eventos_outbox_creado
    ON eventos_outbox(creado_en);

CREATE INDEX IF NOT EXISTS idx_eventos_outbox_pendientes
    ON eventos_outbox(codigo_tipo_evento, creado_en)
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
    FROM eventos_outbox e
    WHERE e.procesado_en IS NULL
    ORDER BY e.creado_en
    LIMIT p_tamanio_lote;
END;
$$ LANGUAGE plpgsql;
