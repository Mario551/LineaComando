using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.DAO;
using System.Collections.Concurrent;

namespace PER.Comandos.LineaComandos.Cola.Registro
{
    public class RegistroComandosSqlServer<TRead, TWrite> : IRegistroComandos<TRead, TWrite>
    {
        private readonly string _connectionString;
        private readonly Dictionary<string, IComandoCreador<TRead, TWrite>> _comandosRegistrados;

        private ConcurrentDictionary<string, MetadatosComando> _metadatosComandosRegistrados;
        public IDictionary<string, MetadatosComando> ComandosRegistrados => _metadatosComandosRegistrados;

        public RegistroComandosSqlServer(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _metadatosComandosRegistrados = new ConcurrentDictionary<string, MetadatosComando>();
            _comandosRegistrados = new Dictionary<string, IComandoCreador<TRead, TWrite>>();
        }

        public async Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    ruta_comando as RutaComando,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM per_comandos_registrados
                WHERE activo = 1
                ORDER BY ruta_comando;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var comandosDb = await connection.QueryAsync<ComandoRegistrado>(sql);

            return comandosDb.Select(MapToMetadatos);
        }

        public async Task ConstruirFactoriaAsync(FactoriaComandos<TRead, TWrite> factoria, CancellationToken token = default)
        {
            var comandosActivos = await ObtenerComandosRegistradosAsync(token);
            var rutasActivas = new HashSet<string>(comandosActivos.Select(c => c.RutaComando));

            var nodosCreados = new Dictionary<string, Nodo<TRead, TWrite>>();

            foreach (var kvp in _comandosRegistrados)
            {
                var ruta = kvp.Key;
                var creador = kvp.Value;

                var partes = ruta.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                AgregarComandoAFactoria(factoria, partes, creador, nodosCreados);
            }

            var rutasEnMemoria = new HashSet<string>(_comandosRegistrados.Keys);
            var rutasADesactivar = rutasActivas.Except(rutasEnMemoria).ToList();

            if (rutasADesactivar.Any())
            {
                await DesactivarComandosAsync(rutasADesactivar, token);
            }
        }

        public async Task RegistrarComandoAsync(
            MetadatosComando metadatos,
            IComandoCreador<TRead, TWrite> comandoCreador,
            CancellationToken token = default)
        {
            _comandosRegistrados[metadatos.RutaComando] = comandoCreador;
            _metadatosComandosRegistrados.TryAdd(metadatos.RutaComando, metadatos);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            const string sqlExiste = @"
                SELECT id 
                FROM per_comandos_registrados 
                WHERE ruta_comando = @RutaComando;";

            var idExistente = await connection.ExecuteScalarAsync<int?>(sqlExiste, new { metadatos.RutaComando });

            int id;
            if (idExistente.HasValue)
            {
                const string sqlUpdate = @"
                    UPDATE per_comandos_registrados
                    SET activo = 1,
                        actualizado_en = GETDATE()
                    WHERE ruta_comando = @RutaComando;
                    
                    SELECT id FROM per_comandos_registrados WHERE ruta_comando = @RutaComando;";

                id = await connection.ExecuteScalarAsync<int>(sqlUpdate, new { metadatos.RutaComando });
            }
            else
            {
                const string sqlInsert = @"
                    INSERT INTO per_comandos_registrados (
                        ruta_comando,
                        descripcion,
                        activo,
                        creado_en
                    )
                    VALUES (
                        @RutaComando,
                        @Descripcion,
                        1,
                        GETDATE()
                    );
                    
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                id = await connection.ExecuteScalarAsync<int>(sqlInsert, new
                {
                    metadatos.RutaComando,
                    metadatos.Descripcion
                });
            }

            metadatos.Id = id;
        }

        public async Task EliminarRegistroComandoAsync(string rutaComando, CancellationToken token = default)
        {
            _comandosRegistrados.Remove(rutaComando);

            await DesactivarComandosAsync(new[] { rutaComando }, token);
        }

        private void AgregarComandoAFactoria(
            FactoriaComandos<TRead, TWrite> factoria,
            string[] partesRuta,
            IComandoCreador<TRead, TWrite> creador,
            Dictionary<string, Nodo<TRead, TWrite>> nodosCreados)
        {
            if (partesRuta.Length == 0)
                return;

            if (partesRuta.Length == 1)
            {
                var nodo = creador as Nodo<TRead, TWrite>
                    ?? throw new InvalidCastException("El creador del comando debe ser un Nodo<TRead, TWrite>");

                factoria.Add(partesRuta[0], nodo);
                nodosCreados[partesRuta[0]] = nodo;
                return;
            }

            Nodo<TRead, TWrite> nodoActual;

            var primeraRutaParcial = partesRuta[0];
            if (!nodosCreados.TryGetValue(primeraRutaParcial, out nodoActual!))
            {
                nodoActual = factoria.Add(partesRuta[0]);
                nodosCreados[primeraRutaParcial] = nodoActual;
            }

            for (int i = 1; i < partesRuta.Length - 1; i++)
            {
                var rutaParcialActual = string.Join(" ", partesRuta.Take(i + 1));

                if (!nodosCreados.TryGetValue(rutaParcialActual, out var nodoSiguiente))
                {
                    nodoSiguiente = nodoActual.Add(partesRuta[i]);
                    nodosCreados[rutaParcialActual] = nodoSiguiente;
                }

                nodoActual = nodoSiguiente!;
            }

            var nodoComando = creador as Nodo<TRead, TWrite>
                ?? throw new InvalidCastException("El creador del comando debe ser un Nodo<TRead, TWrite>");

            nodoActual.Add(partesRuta[^1], nodoComando);
        }

        private async Task DesactivarComandosAsync(IEnumerable<string> rutas, CancellationToken token)
        {
            const string sql = @"
                UPDATE per_comandos_registrados
                SET activo = 0,
                    actualizado_en = GETDATE()
                WHERE ruta_comando IN (
                    SELECT CAST(value AS NVARCHAR(2048))
                    FROM STRING_SPLIT(@Rutas, ',')
                );";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Rutas = string.Join(",", rutas) });
        }

        private static MetadatosComando MapToMetadatos(ComandoRegistrado dao)
        {
            return new MetadatosComando
            {
                Id = dao.Id,
                RutaComando = dao.RutaComando,
                Descripcion = dao.Descripcion,
                Activo = dao.Activo,
                CreadoEn = dao.CreadoEn
            };
        }
    }
}
