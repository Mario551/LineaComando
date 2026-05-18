using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    [Collection("DatabaseSqlServer")]
    public class FlujoCompletoSqlServerTest : BaseIntegracionSqlServerTest
    {
        private readonly RegistroComandosSqlServer<string, ResultadoComando> _registro;
        private readonly AlmacenColaComandosSqlServer _almacen;

        protected override string PrefijoTest => "flujo_completo_sql_";

        public FlujoCompletoSqlServerTest(DatabaseFixtureSqlServer fixture) : base(fixture)
        {
            _registro = new RegistroComandosSqlServer<string, ResultadoComando>(ConnectionString, Esquema);
            _almacen = new AlmacenColaComandosSqlServer(ConnectionString, Esquema);
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            ComandoPrueba.ResetearContador();
        }

        private static async Task<ResultadoComando> EjecutarComandoAsync(
            IFactoriaComandos<string, ResultadoComando> factoria,
            ComandoEnCola comandoEnCola)
        {
            PER.Comandos.LineaComandos.LineaComando lineaComando = ParsearLineaComando(comandoEnCola);
            PER.Comandos.LineaComandos.Comando.IComando<string, ResultadoComando> comando = factoria.Crear(lineaComando);

            return await comando.EjecutarAsync(comandoEnCola.DatosDeComando ?? string.Empty);
        }

        private static PER.Comandos.LineaComandos.LineaComando ParsearLineaComando(ComandoEnCola comandoEnCola)
        {
            List<string> partes = new List<string>();

            string[] rutaPartes = comandoEnCola.RutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            partes.AddRange(rutaPartes);

            if (!string.IsNullOrWhiteSpace(comandoEnCola.Argumentos))
            {
                string[] argumentosPartes = comandoEnCola.Argumentos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                partes.AddRange(argumentosPartes);
            }

            return new PER.Comandos.LineaComandos.LineaComando(partes);
        }

        [Fact]
        public async Task FlujoCompleto_RegistrarEncolarProcesar_DebeEjecutarComando()
        {
            string ruta = PrefijoTest + "orden procesar";
            MetadatosComando metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Procesa una orden"
            };
            ComandoPrueba comandoPrueba = new ComandoPrueba("Orden procesada exitosamente");
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(comandoPrueba);

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--orderId=123",
                DatosDeComando = "{\"orderId\": 123, \"total\": 500}",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await _almacen.EncolarAsync(comandoEnCola);
            Assert.True(comandoId > 0);

            List<ComandoEnCola> pendientes = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();
            Assert.Single(pendientes);

            ComandoEnCola comandoAProcesar = pendientes.First();
            Assert.Equal(ruta, comandoAProcesar.RutaComando);

            int contadorAntes = ComandoPrueba.ContadorEjecuciones;
            ResultadoComando resultado = await EjecutarComandoAsync(factoria, comandoAProcesar);

            Assert.True(resultado.Exitoso);
            Assert.Equal(contadorAntes + 1, ComandoPrueba.ContadorEjecuciones);

            await _almacen.MarcarComoProcesadoAsync(comandoAProcesar.Id, resultado);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            dynamic comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("completado", (string)comandoDb.estado);
            Assert.NotNull(comandoDb.fecha_ejecucion);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoConParametros_DebeUsarParametros()
        {
            string ruta = PrefijoTest + "calculadora sumar";
            MetadatosComando metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Suma dos numeros"
            };
            ComandoSuma comandoSuma = new ComandoSuma();
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(comandoSuma);

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--a=15 --b=27",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await _almacen.EncolarAsync(comandoEnCola);

            List<ComandoEnCola> pendientes = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();
            ComandoEnCola comando = pendientes.First();

            Assert.Equal(ruta, comando.RutaComando);
            Assert.Contains("--a=15", comando.Argumentos);
            Assert.Contains("--b=27", comando.Argumentos);

            ResultadoComando resultado = await EjecutarComandoAsync(factoria, comando);

            Assert.True(resultado.Exitoso);
            Assert.Contains("42", resultado.Salida?.ToString());

            await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            dynamic comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("completado", (string)comandoDb.estado);
        }

        [Fact]
        public async Task FlujoCompleto_MultipleComandosEnCola_DebeProcesarEnOrden()
        {
            string ruta = PrefijoTest + "batch item";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            List<long> ids = new List<long>();
            for (int i = 1; i <= 5; i++)
            {
                ComandoEnCola comando = new ComandoEnCola
                {
                    RutaComando = ruta,
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.Now.AddMilliseconds(i * 10),
                    Estado = "pendiente",
                    Intentos = 0
                };
                ids.Add(await _almacen.EncolarAsync(comando));
            }

            List<ComandoEnCola> pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            Assert.Equal(5, pendientes.Count);

            for (int i = 0; i < 5; i++)
            {
                Assert.Contains($"--index={i + 1}", pendientes[i].Argumentos);
            }

            int contadorAntes = ComandoPrueba.ContadorEjecuciones;

            foreach (ComandoEnCola comando in pendientes)
            {
                ResultadoComando resultado = await EjecutarComandoAsync(factoria, comando);
                Assert.True(resultado.Exitoso);
                await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);
            }

            Assert.Equal(contadorAntes + 5, ComandoPrueba.ContadorEjecuciones);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            int completados = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {Nombres.ColaComandos} WHERE estado = 'completado' AND ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(5, completados);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoFalla_DebeRegistrarError()
        {
            string ruta = PrefijoTest + "proceso fallido";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("", deberiaFallar: true));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await _almacen.EncolarAsync(comandoEnCola);

            List<ComandoEnCola> pendientes = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();
            ComandoEnCola comando = pendientes.First();

            ResultadoComando resultado = await EjecutarComandoAsync(factoria, comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains("Error simulado", resultado.MensajeError);

            await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            dynamic comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("fallido", (string)comandoDb.estado);
            Assert.Contains("Error simulado", (string)comandoDb.mensaje_error);
            Assert.Equal(1, (int)comandoDb.intentos);
        }

        [Fact]
        public async Task FlujoCompleto_ReintentarComandoFallido_DebeIncrementarIntentos()
        {
            string ruta = PrefijoTest + "reintento test";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("", deberiaFallar: true));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await _almacen.EncolarAsync(comandoEnCola);

            List<ComandoEnCola> pendientes1 = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            ResultadoComando resultado1 = await EjecutarComandoAsync(factoria, pendientes1.First());
            Assert.False(resultado1.Exitoso);

            await _almacen.MarcarComoProcesadoAsync(pendientes1.First().Id, resultado1);

            using (Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion())
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    $"UPDATE {Nombres.ColaComandos} SET estado = 'pendiente', fecha_leido = NULL WHERE id = @Id",
                    new { Id = comandoId });
            }

            List<ComandoEnCola> pendientes2 = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            ResultadoComando resultado2 = await EjecutarComandoAsync(factoria, pendientes2.First());
            Assert.False(resultado2.Exitoso);

            await _almacen.MarcarComoProcesadoAsync(pendientes2.First().Id, resultado2);

            using Microsoft.Data.SqlClient.SqlConnection conn = CrearConexion();
            await conn.OpenAsync();

            int intentos = await conn.ExecuteScalarAsync<int>(
                $"SELECT intentos FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal(2, intentos);
        }

        [Fact]
        public async Task FlujoCompleto_DesactivarComando_NoDebeAparecerEnFactoria()
        {
            string rutaActivo = PrefijoTest + "activo comando";
            string rutaInactivo = PrefijoTest + "inactivo comando";

            MetadatosComando metadatos1 = new MetadatosComando { RutaComando = rutaActivo };
            MetadatosComando metadatos2 = new MetadatosComando { RutaComando = rutaInactivo };

            Nodo<string, ResultadoComando> nodo1 = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            Nodo<string, ResultadoComando> nodo2 = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo1);
            await _registro.RegistrarComandoAsync(metadatos2, nodo2);

            await _registro.EliminarRegistroComandoAsync(rutaInactivo);

            IEnumerable<MetadatosComando> comandosActivos = (await _registro.ObtenerComandosRegistradosAsync())
                .Where(c => c.RutaComando.StartsWith(PrefijoTest));

            Assert.Single(comandosActivos);
            Assert.Equal(rutaActivo, comandosActivos.First().RutaComando);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoConDatosJson_DebePreservarDatos()
        {
            string ruta = PrefijoTest + "json test";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            string datosJson = @"{
                ""cliente"": {
                    ""id"": 12345,
                    ""nombre"": ""Juan Perez"",
                    ""email"": ""juan@example.com""
                },
                ""items"": [
                    {""producto"": ""Laptop"", ""cantidad"": 1, ""precio"": 999.99},
                    {""producto"": ""Mouse"", ""cantidad"": 2, ""precio"": 25.50}
                ],
                ""total"": 1050.99,
                ""fecha"": ""2024-01-15T10:30:00Z""
            }";

            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                DatosDeComando = datosJson,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await _almacen.EncolarAsync(comandoEnCola);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            string datos = await connection.QuerySingleAsync<string>(
                $"SELECT datos_comando FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Contains("Juan Perez", datos);
            Assert.Contains("12345", datos);
            Assert.Contains("Laptop", datos);
            Assert.Contains("1050.99", datos);
        }
    }
}
