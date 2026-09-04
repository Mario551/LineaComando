using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.BaseDatos;
using PER.Comandos.LineaComandos.Cola.DAO;
using PER.Comandos.LineaComandos.Excepcion;
using System.Collections.Concurrent;

namespace PER.Comandos.LineaComandos.Cola.Registro
{
    public class RegistroComandosPostgres<TRead, TWrite> : IRegistroComandos<TRead, TWrite>
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;
        private readonly Dictionary<string, IComandoCreador<TRead, TWrite>> _comandosRegistrados;

        private ConcurrentDictionary<string, MetadatosComando> _metadatosComandosRegistrados;
        public IDictionary<string, MetadatosComando> ComandosRegistrados => _metadatosComandosRegistrados;

        public RegistroComandosPostgres(string connectionString)
            : this(connectionString, "public")
        {
        }

        public RegistroComandosPostgres(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.Postgres(esquema);
            _metadatosComandosRegistrados = new ConcurrentDictionary<string, MetadatosComando>();
            _comandosRegistrados = new Dictionary<string, IComandoCreador<TRead, TWrite>>();
        }

        public async Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(CancellationToken token = default)
        {
            string sql = $@"
                SELECT
                    id as Id,
                    ruta_comando as RutaComando,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM {_nombres.ComandosRegistrados}
                WHERE activo = true
                ORDER BY ruta_comando;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var comandosDb = await connection.QueryAsync<ComandoRegistrado>(sql);

            return comandosDb.Select(MapToMetadatos);
        }

        public async Task ConstruirFactoriaAsync(IFactoriaAbstractaComandos<TRead, TWrite> factoria, CancellationToken token = default)
        {
            var comandosActivos = await ObtenerComandosRegistradosAsync(token);
            var rutasActivas = new HashSet<string>(comandosActivos.Select(c => c.RutaComando));

            var nodosCreadosPorFactoria = new Dictionary<string, Dictionary<string, Nodo<TRead, TWrite>>>(StringComparer.Ordinal);

            foreach (var kvp in _comandosRegistrados)
            {
                var ruta = kvp.Key;
                var creador = kvp.Value;

                var partes = ruta.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length < 2)
                    throw new ErrorDeSintaxisExcepcion($"La ruta '{ruta}' debe contener el nombre de una factoría y un comando.");

                string nombreFactoria = partes[0];
                IFactoriaComandos<TRead, TWrite> factoriaComandos = factoria.Get(nombreFactoria);

                if (!nodosCreadosPorFactoria.TryGetValue(nombreFactoria, out Dictionary<string, Nodo<TRead, TWrite>>? nodosCreados))
                {
                    nodosCreados = new Dictionary<string, Nodo<TRead, TWrite>>(StringComparer.Ordinal);
                    nodosCreadosPorFactoria.Add(nombreFactoria, nodosCreados);
                }

                AgregarComandoAFactoria(factoriaComandos, partes[1..], creador, nodosCreados);
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

            string sql = $@"
                INSERT INTO {_nombres.ComandosRegistrados} AS cr (
                    ruta_comando,
                    descripcion,
                    activo,
                    creado_en
                )
                VALUES (
                    @RutaComando,
                    @Descripcion,
                    true,
                    NOW()
                )
                ON CONFLICT (ruta_comando)
                DO UPDATE SET
                    ruta_comando = EXCLUDED.ruta_comando
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    metadatos.RutaComando,
                    metadatos.Descripcion
                });

            metadatos.Id = id;
        }

        public async Task EliminarRegistroComandoAsync(string rutaComando, CancellationToken token = default)
        {
            _comandosRegistrados.Remove(rutaComando);

            await DesactivarComandosAsync(new[] { rutaComando }, token);
        }

        private void AgregarComandoAFactoria(
            IFactoriaComandos<TRead, TWrite> factoria,
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
            string sql = $@"
                UPDATE {_nombres.ComandosRegistrados}
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
                Activo = dao.Activo,
                CreadoEn = dao.CreadoEn
            };
        }
    }
}
