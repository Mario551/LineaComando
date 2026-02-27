using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Manejador
{
    public class RegistroManejadoresSqlServer : IRegistroManejadores
    {
        private readonly string _connectionString;

        public RegistroManejadoresSqlServer(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<int> RegistrarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default)
        {
            const string sql = @"
                DECLARE @ResultId INT;

                SELECT @ResultId = id FROM per_manejadores_evento WHERE codigo = @Codigo;

                IF @ResultId IS NULL
                BEGIN
                    INSERT INTO per_manejadores_evento (
                        codigo,
                        nombre,
                        descripcion,
                        id_comando_registrado,
                        ruta_comando,
                        argumentos_comando,
                        activo,
                        creado_en
                    )
                    VALUES (
                        @Codigo,
                        @Nombre,
                        @Descripcion,
                        @IdComandoRegistrado,
                        @RutaComando,
                        @ArgumentosComando,
                        @Activo,
                        @CreadoEn
                    );
                    
                    SET @ResultId = SCOPE_IDENTITY();
                END

                SELECT @ResultId;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            int id = await connection.QuerySingleOrDefaultAsync<int>(
                sql,
                new
                {
                    manejador.Codigo,
                    manejador.Nombre,
                    manejador.Descripcion,
                    manejador.IdComandoRegistrado,
                    manejador.RutaComando,
                    manejador.ArgumentosComando,
                    Activo = manejador.Activo ? 1 : 0,
                    manejador.CreadoEn
                });
            
            manejador.Id = id;
            return id;
        }

        public async Task<ManejadorEvento?> ObtenerManejadorPorIdAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    id_comando_registrado,
                    ruta_comando,
                    argumentos_comando,
                    activo,
                    creado_en
                FROM per_manejadores_evento
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultado = await connection.QuerySingleOrDefaultAsync<ManejadorEvento>(sql, new { Id = id });
            
            if (resultado != null)
            {
                resultado.Activo = resultado.Activo;
            }
            
            return resultado;
        }

        public async Task<ManejadorEvento?> ObtenerManejadorPorCodigoAsync(string codigo, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    id_comando_registrado,
                    ruta_comando,
                    argumentos_comando,
                    activo,
                    creado_en
                FROM per_manejadores_evento
                WHERE codigo = @Codigo;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultado = await connection.QuerySingleOrDefaultAsync<ManejadorEvento>(sql, new { Codigo = codigo });
            
            if (resultado != null)
            {
                resultado.Activo = resultado.Activo;
            }
            
            return resultado;
        }

        public async Task<IEnumerable<ManejadorEvento>> ObtenerManejadoresActivosAsync(CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    id_comando_registrado,
                    ruta_comando,
                    argumentos_comando,
                    activo,
                    creado_en
                FROM per_manejadores_evento
                WHERE activo = 1
                ORDER BY codigo;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultados = await connection.QueryAsync<ManejadorEvento>(sql);
            
            foreach (var manejador in resultados)
            {
                manejador.Activo = manejador.Activo;
            }
            
            return resultados;
        }

        public async Task ActualizarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_manejadores_evento
                SET
                    codigo = @Codigo,
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    id_comando_registrado = @IdComandoRegistrado,
                    ruta_comando = @RutaComando,
                    argumentos_comando = @ArgumentosComando,
                    activo = @Activo
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new
            {
                manejador.Id,
                manejador.Codigo,
                manejador.Nombre,
                manejador.Descripcion,
                manejador.IdComandoRegistrado,
                manejador.RutaComando,
                manejador.ArgumentosComando,
                Activo = manejador.Activo ? 1 : 0
            });
        }

        public async Task DesactivarManejadorAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_manejadores_evento
                SET activo = 0
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<int> RegistrarDisparadorAsync(DisparadorManejador disparador, CancellationToken token = default)
        {
            const string sql = @"
                DECLARE @ResultId INT;

                SELECT @ResultId = id FROM per_disparadores_manejador WHERE codigo = @Codigo;

                IF @ResultId IS NULL
                BEGIN
                    INSERT INTO per_disparadores_manejador (
                        manejador_evento_id,
                        codigo,
                        modo_disparo,
                        tipo_evento_id,
                        expresion,
                        activo,
                        prioridad,
                        creado_en
                    )
                    VALUES (
                        @ManejadorEventoId,
                        @Codigo,
                        @ModoDisparo,
                        @TipoEventoId,
                        @Expresion,
                        @Activo,
                        @Prioridad,
                        @CreadoEn
                    );
                    
                    SET @ResultId = SCOPE_IDENTITY();
                END

                SELECT @ResultId;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            int id = await connection.QuerySingleOrDefaultAsync<int>(
                sql,
                new
                {
                    disparador.ManejadorEventoId,
                    disparador.Codigo,
                    disparador.ModoDisparo,
                    disparador.TipoEventoId,
                    disparador.Expresion,
                    Activo = disparador.Activo ? 1 : 0,
                    disparador.Prioridad,
                    disparador.CreadoEn
                });

            return id;
        }

        public async Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresParaEventoAsync(
            string tipoEvento,
            CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    d.id,
                    d.manejador_evento_id,
                    m.id_comando_registrado,
                    m.ruta_comando,
                    m.argumentos_comando,
                    d.modo_disparo,
                    te.codigo,
                    d.codigo,
                    d.expresion,
                    d.activo,
                    d.prioridad,
                    d.creado_en
                FROM per_disparadores_manejador d
                INNER JOIN per_manejadores_evento m ON d.manejador_evento_id = m.id
                INNER JOIN per_tipos_evento te ON d.tipo_evento_id = te.id
                WHERE te.codigo = @TipoEvento
                    AND d.activo = 1
                    AND m.activo = 1
                ORDER BY d.prioridad;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultados = await connection.QueryAsync<ConfiguracionManejador>(sql, new { TipoEvento = tipoEvento });
            
            foreach (var config in resultados)
            {
                config.Activo = config.Activo;
            }
            
            return resultados;
        }

        public async Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresProgramadosAsync(
            CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    d.id,
                    d.manejador_evento_id,
                    m.id_comando_registrado,
                    m.ruta_comando,
                    m.argumentos_comando,
                    d.modo_disparo,
                    d.codigo,
                    d.expresion,
                    d.activo,
                    d.prioridad,
                    d.creado_en,
                    d.ultima_ejecucion
                FROM per_disparadores_manejador d
                INNER JOIN per_manejadores_evento m ON d.manejador_evento_id = m.id
                WHERE d.modo_disparo = 'Programado'
                    AND d.activo = 1
                    AND m.activo = 1
                ORDER BY d.prioridad;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultados = await connection.QueryAsync<ConfiguracionManejador>(sql);
            
            foreach (var config in resultados)
            {
                config.Activo = config.Activo;
            }
            
            return resultados;
        }

        public async Task ActualizarConfiguracionAsync(ConfiguracionManejador configuracion, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_disparadores_manejador
                SET
                    modo_disparo = @ModoDisparo,
                    expresion = @Expresion,
                    activo = @Activo,
                    prioridad = @Prioridad
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new
            {
                configuracion.Id,
                configuracion.ModoDisparo,
                configuracion.Expresion,
                Activo = configuracion.Activo ? 1 : 0,
                configuracion.Prioridad
            });
        }


        public async Task ActualizarUltimaEjecucionAsync(int disparadorId, DateTime ultimaEjecucion, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_disparadores_manejador
                SET ultima_ejecucion = @UltimaEjecucion
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = disparadorId, UltimaEjecucion = ultimaEjecucion });
        }
    }
}
