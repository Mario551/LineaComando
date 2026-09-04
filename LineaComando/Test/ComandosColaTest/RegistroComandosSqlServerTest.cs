using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Excepcion;
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
            var ruta = PrefijoTest + "modulo insertar";
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
            var ruta = PrefijoTest + "modulo no_duplicar";
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
            var rutaActivo = PrefijoTest + "modulo activo";
            var rutaInactivo = PrefijoTest + "modulo inactivo";
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
            var ruta = PrefijoTest + "modulo eliminar";
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
            var nombreFactoria = PrefijoTest + "orden";
            var rutaCrear = nombreFactoria + " crear";
            var rutaPagar = nombreFactoria + " pagar";

            var metadatosCrear = new MetadatosComando { RutaComando = rutaCrear };
            var metadatosPagar = new MetadatosComando { RutaComando = rutaPagar };

            var nodoCrear = new Nodo<string, ResultadoComando>(new ComandoPrueba("Orden creada"));
            var nodoPagar = new Nodo<string, ResultadoComando>(new ComandoPrueba("Orden pagada"));

            await _registro.RegistrarComandoAsync(metadatosCrear, nodoCrear);
            await _registro.RegistrarComandoAsync(metadatosPagar, nodoPagar);

            FactoriaComandos<string, ResultadoComando> factoriaComandos = new(nombreFactoria);
            FactoriaAbstractaComandos<string, ResultadoComando> factoria = new([factoriaComandos]);

            await _registro.ConstruirFactoriaAsync(factoria);

            var comandos = (await _registro.ObtenerComandosRegistradosAsync())
                .Where(c => c.RutaComando.StartsWith(PrefijoTest));
            ResultadoComando resultadoCrear = await factoria
                .Crear(new LineaComando(rutaCrear.Split(' ')))
                .EjecutarAsync(string.Empty);
            ResultadoComando resultadoPagar = await factoria
                .Crear(new LineaComando(rutaPagar.Split(' ')))
                .EjecutarAsync(string.Empty);

            Assert.Equal(2, comandos.Count());
            Assert.Equal("Orden creada", resultadoCrear.Salida);
            Assert.Equal("Orden pagada", resultadoPagar.Salida);
        }

        [Fact]
        public async Task ConstruirFactoriaAsync_ConMismaRutaLocal_DebeSepararFactorias()
        {
            string nombrePedidos = PrefijoTest + "pedidos";
            string nombreClientes = PrefijoTest + "clientes";
            await _registro.RegistrarComandoAsync(
                new MetadatosComando { RutaComando = nombrePedidos + " consultar" },
                new Nodo<string, ResultadoComando>(new ComandoPrueba("pedido")));
            await _registro.RegistrarComandoAsync(
                new MetadatosComando { RutaComando = nombreClientes + " consultar" },
                new Nodo<string, ResultadoComando>(new ComandoPrueba("cliente")));
            FactoriaAbstractaComandos<string, ResultadoComando> factoria = new(
                [
                    new FactoriaComandos<string, ResultadoComando>(nombrePedidos),
                    new FactoriaComandos<string, ResultadoComando>(nombreClientes)
                ]);

            await _registro.ConstruirFactoriaAsync(factoria);

            ResultadoComando pedido = await factoria
                .Crear(new LineaComando([nombrePedidos, "consultar"]))
                .EjecutarAsync(string.Empty);
            ResultadoComando cliente = await factoria
                .Crear(new LineaComando([nombreClientes, "consultar"]))
                .EjecutarAsync(string.Empty);
            Assert.Equal("pedido", pedido.Salida);
            Assert.Equal("cliente", cliente.Salida);
        }

        [Fact]
        public async Task ConstruirFactoriaAsync_ConRutaSinComando_DebeLanzarExcepcion()
        {
            string nombreFactoria = PrefijoTest + "incompleta";
            await _registro.RegistrarComandoAsync(
                new MetadatosComando { RutaComando = nombreFactoria },
                new Nodo<string, ResultadoComando>(new ComandoPrueba()));
            FactoriaAbstractaComandos<string, ResultadoComando> factoria = new(
                [new FactoriaComandos<string, ResultadoComando>(nombreFactoria)]);

            await Assert.ThrowsAsync<ErrorDeSintaxisExcepcion>(() =>
                _registro.ConstruirFactoriaAsync(factoria));
        }
    }
}
