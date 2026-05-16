-- Tabla: per_comandos_registrados
-- Almacena el catálogo de comandos disponibles en el sistema

IF OBJECT_ID('per_comandos_registrados', 'U') IS NULL
BEGIN
    CREATE TABLE per_comandos_registrados (
        id INT IDENTITY(1,1) PRIMARY KEY,
        ruta_comando NVARCHAR(2048) NOT NULL,
        descripcion NVARCHAR(2048) NULL,
        activo INT NOT NULL DEFAULT 1,
        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
        actualizado_en DATETIME2 NULL,
        CONSTRAINT uq_per_comandos_registrados_ruta UNIQUE (ruta_comando)
    );

    CREATE INDEX idx_per_comandos_registrados_ruta 
        ON per_comandos_registrados(ruta_comando);

    CREATE INDEX idx_per_comandos_registrados_activo 
        ON per_comandos_registrados(activo);
END

-- Tabla: per_cola_comandos_estados
-- Catálogo de estados válidos para la cola de comandos

IF OBJECT_ID('per_cola_comandos_estados', 'U') IS NULL
BEGIN
    CREATE TABLE per_cola_comandos_estados (
        estado NVARCHAR(50) NOT NULL,
        descripcion NVARCHAR(200) NOT NULL,
        CONSTRAINT pk_per_cola_comandos_estados PRIMARY KEY (estado)
    );
END

MERGE per_cola_comandos_estados AS destino
USING (VALUES
    ('pendiente', 'Comando registrado y pendiente de tomar.'),
    ('procesando', 'Comando tomado por un worker.'),
    ('completado', 'Comando ejecutado correctamente.'),
    ('fallido', 'Comando terminado con error.')
) AS origen (estado, descripcion)
ON destino.estado = origen.estado
WHEN MATCHED THEN
    UPDATE SET descripcion = origen.descripcion
WHEN NOT MATCHED THEN
    INSERT (estado, descripcion)
    VALUES (origen.estado, origen.descripcion);

-- Tabla: per_cola_comandos
-- Representa la cola de ejecución de comandos con relación a comandos registrados

IF OBJECT_ID('per_cola_comandos', 'U') IS NULL
BEGIN
    CREATE TABLE per_cola_comandos (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        id_comando_registrado INT NOT NULL,
        ruta_comando NVARCHAR(2048) NOT NULL,
        argumentos NVARCHAR(2048) NULL,
        datos_comando NVARCHAR(MAX) NULL,
        fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        fecha_leido DATETIME2 NULL,
        fecha_ejecucion DATETIME2 NULL,
        estado NVARCHAR(50) NOT NULL CONSTRAINT df_per_cola_comandos_estado DEFAULT 'pendiente',
        mensaje_error NVARCHAR(MAX) NULL,
        duracion_ms BIGINT NULL,
        intentos INT NOT NULL DEFAULT 0,

        CONSTRAINT fk_cola_comandos_comando_registrado
            FOREIGN KEY (id_comando_registrado)
            REFERENCES per_comandos_registrados(id)
            ON DELETE NO ACTION,

        CONSTRAINT fk_per_cola_comandos_estado
            FOREIGN KEY (estado)
            REFERENCES per_cola_comandos_estados(estado)
            ON DELETE NO ACTION
    );

    CREATE INDEX idx_per_cola_comandos_estado 
        ON per_cola_comandos(estado);

    CREATE INDEX idx_per_cola_comandos_fecha_creacion 
        ON per_cola_comandos(fecha_creacion);

    CREATE INDEX idx_per_cola_comandos_fecha_leido 
        ON per_cola_comandos(fecha_leido) 
        WHERE fecha_leido IS NOT NULL;

    CREATE INDEX idx_per_cola_comandos_pendientes 
        ON per_cola_comandos(id, fecha_creacion) 
        WHERE estado = 'pendiente' AND fecha_leido IS NULL;
END

-- Tabla: per_cola_comandos_resultados
-- Payload durable de resultados de comandos completados

IF OBJECT_ID('per_cola_comandos_resultados', 'U') IS NULL
BEGIN
    CREATE TABLE per_cola_comandos_resultados (
        comando_id BIGINT NOT NULL,
        tipo NVARCHAR(200) NOT NULL,
        version_resultado INT NOT NULL,
        formato NVARCHAR(100) NOT NULL,
        payload NVARCHAR(MAX) NULL,
        ruta_payload NVARCHAR(2048) NULL,
        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),

        CONSTRAINT pk_per_cola_comandos_resultados PRIMARY KEY (comando_id),

        CONSTRAINT fk_per_cola_comandos_resultados_comando
            FOREIGN KEY (comando_id)
            REFERENCES per_cola_comandos(id)
            ON DELETE CASCADE,

        CONSTRAINT ck_per_cola_comandos_resultados_payload_o_ruta
            CHECK (
                (payload IS NOT NULL AND ruta_payload IS NULL)
                OR
                (payload IS NULL AND ruta_payload IS NOT NULL)
            )
    );

    CREATE INDEX idx_per_cola_comandos_resultados_tipo_version
        ON per_cola_comandos_resultados(tipo, version_resultado);
END

-- Función para obtener comandos pendientes (solo lectura)
CREATE OR ALTER FUNCTION obtener_comandos_pendientes(
    @tamanio_lote INT = 50,
    @timeout_milisegundos INT = 300000
)
RETURNS TABLE
AS
RETURN
    SELECT 
        c.id,
        c.id_comando_registrado,
        c.ruta_comando,
        c.argumentos,
        c.datos_comando,
        c.fecha_creacion,
        c.fecha_leido,
        c.fecha_ejecucion,
        c.estado,
        c.mensaje_error,
        c.duracion_ms,
        c.intentos
    FROM per_cola_comandos c
    WHERE (
        (c.fecha_leido IS NULL AND c.estado = 'pendiente')
        OR
        (c.estado = 'procesando' AND c.fecha_leido < DATEADD(MILLISECOND, -@timeout_milisegundos, GETDATE()))
    );

-- Procedimiento para marcar comandos como procesando
CREATE OR ALTER PROCEDURE marcar_comandos_procesando
    @ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @IdTable TABLE (id BIGINT);
    
    INSERT INTO @IdTable (id)
    SELECT CAST(value AS BIGINT)
    FROM STRING_SPLIT(@ids, ',');
    
    UPDATE c
    SET fecha_leido = GETDATE(),
        estado = 'procesando'
    FROM per_cola_comandos c
    INNER JOIN @IdTable t ON c.id = t.id;
    
    SELECT 
        c.id,
        c.id_comando_registrado,
        c.ruta_comando,
        c.argumentos,
        c.datos_comando,
        c.fecha_creacion,
        c.fecha_leido,
        c.fecha_ejecucion,
        c.estado,
        c.mensaje_error,
        c.duracion_ms,
        c.intentos
    FROM per_cola_comandos c
    INNER JOIN @IdTable t ON c.id = t.id;
END

-- Procedimiento para actualizar fecha de lectura de comandos
CREATE OR ALTER PROCEDURE actualizar_fecha_leido
    @ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @IdTable TABLE (id BIGINT);
    
    INSERT INTO @IdTable (id)
    SELECT CAST(value AS BIGINT)
    FROM STRING_SPLIT(@ids, ',');
    
    UPDATE c
    SET fecha_leido = GETDATE()
    FROM per_cola_comandos c
    INNER JOIN @IdTable t ON c.id = t.id
    WHERE c.fecha_leido IS NULL;
END
