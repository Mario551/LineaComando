using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.DAO;

namespace PER.Comandos.LineaComandos.Persistence.Registro
{
    public class RegistroComandos<TRead, TWrite> : IRegistroComandos<TRead, TWrite>
    {
        private readonly string _connectionString;
        private readonly Dictionary<string, IComandoCreador<TRead, TWrite>> _comandosRegistrados;

        public RegistroComandos(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _comandosRegistrados = new Dictionary<string, IComandoCreador<TRead, TWrite>>();
        }

        public async Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    ruta_comando as RutaComando,
                    descripcion as Descripcion,
                    esquema_parametros as EsquemaParametros,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM comandos_registrados
                WHERE activo = true
                ORDER BY ruta_comando;";

            using var connection = new NpgsqlConnection(_connectionString);
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

            const string sql = @"
                INSERT INTO comandos_registrados (
                    ruta_comando,
                    descripcion,
                    esquema_parametros,
                    activo,
                    creado_en
                )
                VALUES (
                    @RutaComando,
                    @Descripcion,
                    @EsquemaParametros::jsonb,
                    true,
                    NOW()
                )
                ON CONFLICT (ruta_comando)
                DO UPDATE SET
                    activo = true,
                    actualizado_en = NOW(),
                    descripcion = EXCLUDED.descripcion,
                    esquema_parametros = EXCLUDED.esquema_parametros
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    metadatos.RutaComando,
                    metadatos.Descripcion,
                    metadatos.EsquemaParametros
                });

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
                UPDATE comandos_registrados
                SET activo = false,
                    actualizado_en = NOW()
                WHERE ruta_comando = ANY(@Rutas);";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Rutas = rutas.ToArray() });
        }

        private static MetadatosComando MapToMetadatos(ComandoRegistrado dao)
        {
            return new MetadatosComando
            {
                Id = dao.Id,
                RutaComando = dao.RutaComando,
                Descripcion = dao.Descripcion,
                EsquemaParametros = dao.EsquemaParametros,
                Activo = dao.Activo,
                CreadoEn = dao.CreadoEn
            };
        }
    }
}
