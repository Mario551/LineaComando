-- Tabla: per_tipos_evento
-- Catálogo de tipos de eventos disponibles en el sistema

IF OBJECT_ID('dbo.per_tipos_evento', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.per_tipos_evento (
        id INT IDENTITY(1,1) PRIMARY KEY,
        codigo NVARCHAR(255) NOT NULL UNIQUE,
        nombre NVARCHAR(255) NOT NULL,
        descripcion NVARCHAR(2048) NULL,
        activo INT NOT NULL DEFAULT 1,
        creado_en DATETIME2 NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX idx_per_tipos_evento_codigo
        ON dbo.per_tipos_evento(codigo);

    CREATE INDEX idx_per_tipos_evento_activo
        ON dbo.per_tipos_evento(activo);
END

-- Tabla: per_manejadores_evento
-- Manejadores de eventos vinculados a comandos registrados

IF OBJECT_ID('dbo.per_manejadores_evento', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.per_manejadores_evento (
        id INT IDENTITY(1,1) PRIMARY KEY,
        codigo NVARCHAR(255) NOT NULL UNIQUE,
        nombre NVARCHAR(255) NOT NULL,
        descripcion NVARCHAR(2048) NULL,
        id_comando_registrado INT NOT NULL,
        ruta_comando NVARCHAR(2048) NOT NULL,
        argumentos_comando NVARCHAR(2048) NULL,
        activo INT NOT NULL DEFAULT 1,
        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),

        CONSTRAINT fk_manejador_comando
            FOREIGN KEY (id_comando_registrado)
            REFERENCES dbo.per_comandos_registrados(id)
            ON DELETE NO ACTION
    );

    CREATE INDEX idx_per_manejadores_evento_codigo
        ON dbo.per_manejadores_evento(codigo);

    CREATE INDEX idx_per_manejadores_evento_activo
        ON dbo.per_manejadores_evento(activo);

    CREATE INDEX idx_per_manejadores_evento_comando
        ON dbo.per_manejadores_evento(id_comando_registrado);
END

-- Tabla: per_disparadores_manejador
-- Disparadores que activan los manejadores (por evento o programado)

IF OBJECT_ID('dbo.per_disparadores_manejador', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.per_disparadores_manejador (
        id INT IDENTITY(1,1) PRIMARY KEY,
        codigo NVARCHAR(255) NOT NULL UNIQUE,
        manejador_evento_id INT NOT NULL,
        modo_disparo NVARCHAR(50) NOT NULL DEFAULT 'Evento',
        tipo_evento_id INT NULL,
        expresion NVARCHAR(255) NULL,
        activo INT NOT NULL DEFAULT 1,
        prioridad INT NOT NULL DEFAULT 0,
        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
        ultima_ejecucion DATETIME2 NULL,

        CONSTRAINT fk_disparador_manejador
            FOREIGN KEY (manejador_evento_id)
            REFERENCES dbo.per_manejadores_evento(id)
            ON DELETE CASCADE,

        CONSTRAINT fk_disparador_tipo_evento
            FOREIGN KEY (tipo_evento_id)
            REFERENCES dbo.per_tipos_evento(id)
            ON DELETE CASCADE,

        CONSTRAINT chk_modo_disparo
            CHECK (modo_disparo IN ('Evento', 'Programado')),

        CONSTRAINT chk_disparador_valido
            CHECK (
                (modo_disparo = 'Evento' AND tipo_evento_id IS NOT NULL) OR
                (modo_disparo = 'Programado' AND expresion IS NOT NULL)
            )
    );

    CREATE INDEX idx_per_disparadores_manejador_evento_id
        ON dbo.per_disparadores_manejador(manejador_evento_id);

    CREATE INDEX idx_disparadores_tipo_evento
        ON dbo.per_disparadores_manejador(tipo_evento_id)
        WHERE tipo_evento_id IS NOT NULL;

    CREATE INDEX idx_disparadores_modo
        ON dbo.per_disparadores_manejador(modo_disparo, activo);

    CREATE INDEX idx_disparadores_programados
        ON dbo.per_disparadores_manejador(modo_disparo, activo, expresion)
        WHERE modo_disparo = 'Programado';
END

-- Tabla: per_eventos_outbox
-- Cola de eventos pendientes de procesar (patrón Outbox)

IF OBJECT_ID('dbo.per_eventos_outbox', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.per_eventos_outbox (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        codigo_tipo_evento NVARCHAR(255) NOT NULL,
        agregado_id BIGINT NULL,
        datos_evento NVARCHAR(MAX) NOT NULL,
        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
        procesado_en DATETIME2 NULL
    );

    CREATE INDEX idx_per_eventos_outbox_tipo
        ON dbo.per_eventos_outbox(codigo_tipo_evento);

    CREATE INDEX idx_per_eventos_outbox_procesado
        ON dbo.per_eventos_outbox(procesado_en)
        WHERE procesado_en IS NULL;

    CREATE INDEX idx_per_eventos_outbox_creado
        ON dbo.per_eventos_outbox(creado_en);

    CREATE INDEX idx_per_eventos_outbox_pendientes
        ON dbo.per_eventos_outbox(codigo_tipo_evento, creado_en)
        WHERE procesado_en IS NULL;
END

GO

-- Función para obtener eventos pendientes del outbox
CREATE OR ALTER FUNCTION dbo.obtener_eventos_pendientes(
    @tamanio_lote INT = 50
)
RETURNS TABLE
AS
RETURN
    SELECT TOP (@tamanio_lote)
        id,
        codigo_tipo_evento,
        agregado_id,
        datos_evento,
        creado_en,
        procesado_en
    FROM dbo.per_eventos_outbox
    WHERE procesado_en IS NULL
    ORDER BY creado_en;

GO
