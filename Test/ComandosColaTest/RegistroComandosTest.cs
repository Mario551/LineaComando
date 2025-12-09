using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    [Collection("Database")]
    public class RegistroComandosTest : BaseIntegracionTest
    {
        private readonly RegistroComandos<string, ResultadoComando> _registro;

        protected override string PrefijoTest => "registro_cmd_";

        public RegistroComandosTest(DatabaseFixture fixture) : base(fixture)
        {
            _registro = new RegistroComandos<string, ResultadoComando>(ConnectionString);
        }

        [Fact]
        public async Task RegistrarComandoAsync_DebeInsertarComando()
        {
            var ruta = PrefijoTest + "insertar";
            var metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Comando de prueba",
                EsquemaParametros = "{\"parametros\": []}"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.NotNull(comandoDb);
            Assert.Equal("Comando de prueba", (string)comandoDb.descripcion);
            Assert.True((bool)comandoDb.activo);
            Assert.True(metadatos.Id > 0);
        }

        [Fact]
        public async Task RegistrarComandoAsync_DebeActualizarComandoExistente()
        {
            var ruta = PrefijoTest + "actualizar";
            var metadatos1 = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Version 1"
            };
            var metadatos2 = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Version 2 actualizada"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo);
            await _registro.RegistrarComandoAsync(metadatos2, nodo);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(1, count);
            Assert.Equal("Version 2 actualizada", (string)comandoDb.descripcion);
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
                    "UPDATE comandos_registrados SET activo = false WHERE ruta_comando = @Ruta",
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
                "SELECT * FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.NotNull(comandoDb);
            Assert.False((bool)comandoDb.activo);
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

        [Fact]
        public async Task RegistrarComandoAsync_ConEsquemaParametros_DebeGuardarJsonb()
        {
            var ruta = PrefijoTest + "con_esquema";
            var esquema = @"{
                ""parametros"": [
                    {""nombre"": ""orderId"", ""tipo"": ""int"", ""requerido"": true},
                    {""nombre"": ""monto"", ""tipo"": ""decimal"", ""requerido"": true}
                ]
            }";
            var metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Procesa un pago",
                EsquemaParametros = esquema
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT esquema_parametros::text as esquema FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.NotNull(comandoDb.esquema);
            Assert.Contains("orderId", (string)comandoDb.esquema);
        }
    }
}
