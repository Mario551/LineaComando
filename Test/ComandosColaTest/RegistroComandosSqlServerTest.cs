using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    [Collection("DatabaseSqlServer")]
    public class RegistroComandosSqlServerTest : BaseIntegracionSqlServerTest
    {
        private readonly RegistroComandosSqlServer<string, ResultadoComando> _registro;

        protected override string PrefijoTest => "registro_cmd_";

        public RegistroComandosSqlServerTest(DatabaseFixtureSqlServer fixture) : base(fixture)
        {
            _registro = new RegistroComandosSqlServer<string, ResultadoComando>(ConnectionString, Esquema);
        }

        [Fact]
        public async Task RegistrarComandoAsync_DebeInsertarComando()
        {
            var ruta = PrefijoTest + "insertar";
            var metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Comando de prueba"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                $"SELECT * FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.NotNull(comandoDb);
            Assert.Equal("Comando de prueba", (string)comandoDb.descripcion);
            Assert.Equal(1, (int)comandoDb.activo);
            Assert.True(metadatos.Id > 0);
        }

        [Fact]
        public async Task RegistrarComandoAsync_NoDebeDuplicarComandoExistente()
        {
            var ruta = PrefijoTest + "no_duplicar";
            var metadatos1 = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Version 1"
            };
            var metadatos2 = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Version 2"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo);
            await _registro.RegistrarComandoAsync(metadatos2, nodo);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var count = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(1, count);
            Assert.Equal("Version 1", (string)comandoDb.descripcion);
        }

        [Fact]
        public async Task ObtenerComandosRegistradosAsync_DebeRetornarSoloActivos()
        {
            var rutaActivo = PrefijoTest + "activo";
            var rutaInactivo = PrefijoTest + "inactivo";
            var metadatos1 = new MetadatosComando { RutaComando = rutaInactivo };
            var metadatos2 = new MetadatosComando { RutaComando = rutaActivo };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo);
            await _registro.RegistrarComandoAsync(metadatos2, nodo);

            using (var connection = CrearConexion())
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    $"UPDATE {Nombres.ComandosRegistrados} SET activo = 0 WHERE ruta_comando = @Ruta",
                    new { Ruta = rutaInactivo });
            }

            var comandos = (await _registro.ObtenerComandosRegistradosAsync())
                .Where(c => c.RutaComando.StartsWith(PrefijoTest));

            Assert.Single(comandos);
            Assert.Equal(rutaActivo, comandos.First().RutaComando);
        }

        [Fact]
        public async Task EliminarRegistroComandoAsync_DebeDesactivarComando()
        {
            var ruta = PrefijoTest + "eliminar";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            await _registro.EliminarRegistroComandoAsync(ruta);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                $"SELECT * FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.NotNull(comandoDb);
            Assert.Equal(0, (int)comandoDb.activo);
        }

        [Fact]
        public async Task ConstruirFactoriaAsync_DebeConstruirArbolDeComandos()
        {
            var rutaBase = PrefijoTest + "orden";
            var rutaCrear = PrefijoTest + "orden crear";
            var rutaPagar = PrefijoTest + "orden pagar";

            var metadatos1 = new MetadatosComando { RutaComando = rutaBase };
            var metadatos2 = new MetadatosComando { RutaComando = rutaCrear };
            var metadatos3 = new MetadatosComando { RutaComando = rutaPagar };

            var nodoOrden = new Nodo<string, ResultadoComando>();
            var nodoCrear = new Nodo<string, ResultadoComando>(new ComandoPrueba("Orden creada"));
            var nodoPagar = new Nodo<string, ResultadoComando>(new ComandoPrueba("Orden pagada"));

            await _registro.RegistrarComandoAsync(metadatos1, nodoOrden);
            await _registro.RegistrarComandoAsync(metadatos2, nodoCrear);
            await _registro.RegistrarComandoAsync(metadatos3, nodoPagar);

            var factoria = new FactoriaComandos<string, ResultadoComando>();

            await _registro.ConstruirFactoriaAsync(factoria);

            var comandos = (await _registro.ObtenerComandosRegistradosAsync())
                .Where(c => c.RutaComando.StartsWith(PrefijoTest));
            Assert.Equal(3, comandos.Count());
        }

    }
}
