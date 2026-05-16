using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Registro;
using Npgsql;

namespace ComandosColaTest
{
    [Collection("Database")]
    public class AlmacenColaComandosPostgresTest : BaseIntegracionPostgresTest
    {
        private readonly AlmacenColaComandosPostgres _almacen;
        private readonly RegistroComandosPostgres<string, ResultadoComando> _registro;

        protected override string PrefijoTest => "almacen_cola_";

        public AlmacenColaComandosPostgresTest(DatabaseFixture fixture) : base(fixture)
        {
            _almacen = new AlmacenColaComandosPostgres(ConnectionString, Esquema);
            _registro = new RegistroComandosPostgres<string, ResultadoComando>(ConnectionString, Esquema);
        }

        private async Task PrepararTestAsync(string rutaComando)
        {
            var metadatos = new MetadatosComando
            {
                RutaComando = rutaComando,
                Descripcion = $"Comando de prueba: {rutaComando}"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);
        }

        [Fact]
        public async Task EncolarAsync_DebeInsertarComandoEnCola()
        {
            var ruta = PrefijoTest + "encolar_insertar";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--mensaje=hola",
                DatosDeComando = "{\"key\": \"value\"}",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            Assert.True(id > 0);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = id });

            Assert.NotNull(comandoDb);
            Assert.Equal(ruta, (string)comandoDb.ruta_comando);
            Assert.Equal("--mensaje=hola", (string)comandoDb.argumentos);
            Assert.Equal("pendiente", (string)comandoDb.estado);
        }

        [Fact]
        public async Task EstadosColaComandos_DebeCrearCatalogoBase()
        {
            using var connection = CrearConexion();
            await connection.OpenAsync();

            IEnumerable<string> estados = await connection.QueryAsync<string>(
                $"SELECT estado FROM {Nombres.ColaComandosEstados} ORDER BY estado;");

            Assert.Equal(
                new[] { "completado", "fallido", "pendiente", "procesando" },
                estados.ToArray());
        }

        [Fact]
        public async Task EncolarAsync_ConEstadoInvalido_DebeFallarPorLlaveForanea()
        {
            string ruta = PrefijoTest + "estado_invalido";
            await PrepararTestAsync(ruta);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            int comandoRegistradoId = await connection.ExecuteScalarAsync<int>(
                $"SELECT id FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
                new { Ruta = ruta });

            await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
                $@"
                INSERT INTO {Nombres.ColaComandos} (
                    id_comando_registrado,
                    ruta_comando,
                    fecha_creacion,
                    estado,
                    intentos
                )
                VALUES (
                    @IdComandoRegistrado,
                    @Ruta,
                    NOW(),
                    @Estado,
                    0
                );",
                new
                {
                    IdComandoRegistrado = comandoRegistradoId,
                    Ruta = ruta,
                    Estado = "invalido"
                }));
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeRetornarComandosPendientes()
        {
            var ruta = PrefijoTest + "obtener_pendientes";
            await PrepararTestAsync(ruta);

            var comando1 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--id=1",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };
            var comando2 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--id=2",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long c1 = await _almacen.EncolarAsync(comando1);
            long c2 = await _almacen.EncolarAsync(comando2);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            Assert.Equal(2, pendientes.Count);
            Assert.Contains(c1, pendientes.Select(e => e.Id));
            Assert.Contains(c2, pendientes.Select(e => e.Id));
            Assert.All(pendientes, c => Assert.Equal("pendiente", c.Estado));
        }

        [Fact]
        public async Task MarcarComandosProcesandoAsync_DebeMarcarComoProcesando()
        {
            var ruta = PrefijoTest + "marcar_procesando";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--test=true",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            var procesando = await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            Assert.Single(procesando);
            Assert.Equal("procesando", procesando.First().Estado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = id });

            Assert.Equal("procesando", (string)comandoDb.estado);
            Assert.NotNull(comandoDb.fecha_leido);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_NoDebeRetornarComandosYaMarcados()
        {
            var ruta = PrefijoTest + "no_retornar_marcados";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta);

            await _almacen.MarcarComandosProcesandoAsync(pendientes.Select(c => c.Id).ToArray());

            var pendientesSegundaLectura = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta);

            Assert.Empty(pendientesSegundaLectura);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_DebeActualizarEstadoExitoso()
        {
            var ruta = PrefijoTest + "estado_exitoso";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);
            await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            var resultado = ResultadoComando.Exito("Procesado correctamente", TimeSpan.FromMilliseconds(150));

            await _almacen.MarcarComoProcesadoAsync(id, resultado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = id });

            Assert.Equal("completado", (string)comandoDb.estado);
            Assert.NotNull(comandoDb.fecha_ejecucion);
            Assert.Equal(150, (long)comandoDb.duracion_ms);
            Assert.Null(comandoDb.mensaje_error);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_ConPayloadResultado_DebePersistirResultado()
        {
            string ruta = PrefijoTest + "resultado_payload";
            await PrepararTestAsync(ruta);

            ComandoEnCola comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long id = await _almacen.EncolarAsync(comando);
            await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            ResultadoComando resultado = ResultadoComando.Exito("salida durable", TimeSpan.FromMilliseconds(75));
            PayloadResultadoComando payload = new PayloadResultadoComando
            {
                Tipo = "texto",
                Version = 1,
                Formato = "text/plain",
                Contenido = "salida durable"
            };

            await _almacen.MarcarComoProcesadoAsync(id, resultado, payload);

            ResultadoComandoPersistido? resultadoPersistido = await _almacen.ObtenerResultadoPersistidoAsync(id);

            Assert.NotNull(resultadoPersistido);
            Assert.Equal("completado", resultadoPersistido.Estado);
            Assert.Equal(TimeSpan.FromMilliseconds(75), resultadoPersistido.Duracion);
            Assert.NotNull(resultadoPersistido.PayloadResultado);
            Assert.Equal("texto", resultadoPersistido.PayloadResultado.Tipo);
            Assert.Equal(1, resultadoPersistido.PayloadResultado.Version);
            Assert.Equal("text/plain", resultadoPersistido.PayloadResultado.Formato);
            Assert.Equal("salida durable", resultadoPersistido.PayloadResultado.Contenido);
            Assert.Null(resultadoPersistido.PayloadResultado.RutaPayload);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_ConRutaPayloadResultado_DebePersistirRuta()
        {
            string ruta = PrefijoTest + "resultado_ruta_payload";
            await PrepararTestAsync(ruta);

            ComandoEnCola comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long id = await _almacen.EncolarAsync(comando);
            await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            ResultadoComando resultado = ResultadoComando.Exito("salida durable", TimeSpan.FromMilliseconds(75));
            PayloadResultadoComando payload = new PayloadResultadoComando
            {
                Tipo = "texto",
                Version = 1,
                Formato = "text/plain",
                RutaPayload = "texto/v1/10.11111111111111111111111111111111.payload"
            };

            await _almacen.MarcarComoProcesadoAsync(id, resultado, payload);

            ResultadoComandoPersistido? resultadoPersistido = await _almacen.ObtenerResultadoPersistidoAsync(id);

            Assert.NotNull(resultadoPersistido);
            Assert.NotNull(resultadoPersistido.PayloadResultado);
            Assert.Null(resultadoPersistido.PayloadResultado.Contenido);
            Assert.Equal("texto/v1/10.11111111111111111111111111111111.payload", resultadoPersistido.PayloadResultado.RutaPayload);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_DebeActualizarEstadoFallido()
        {
            var ruta = PrefijoTest + "estado_fallido";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);
            await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            var resultado = ResultadoComando.Fallo("Error de conexión", TimeSpan.FromMilliseconds(50));

            await _almacen.MarcarComoProcesadoAsync(id, resultado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = id });

            Assert.Equal("fallido", (string)comandoDb.estado);
            Assert.Equal("Error de conexión", (string)comandoDb.mensaje_error);
            Assert.Equal(1, (int)comandoDb.intentos);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeRespetarTamanioLote()
        {
            var ruta = PrefijoTest + "tamanio_lote";
            await PrepararTestAsync(ruta);

            for (int i = 0; i < 10; i++)
            {
                var comando = new ComandoEnCola
                {
                    RutaComando = ruta,
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.Now,
                    Estado = "pendiente",
                    Intentos = 0
                };
                await _almacen.EncolarAsync(comando);
            }

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .Take(3);

            Assert.Equal(3, pendientes.Count());
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeOrdenarPorFechaCreacion()
        {
            var ruta = PrefijoTest + "ordenar_fecha";
            await PrepararTestAsync(ruta);

            var fechaBase = DateTime.Now;

            var comando1 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--orden=primero",
                FechaCreacion = fechaBase.AddSeconds(1),
                Estado = "pendiente",
                Intentos = 0
            };
            var comando2 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--orden=segundo",
                FechaCreacion = fechaBase,
                Estado = "pendiente",
                Intentos = 0
            };

            await _almacen.EncolarAsync(comando1);
            await _almacen.EncolarAsync(comando2);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            Assert.Equal(2, pendientes.Count);
            Assert.Contains("--orden=primero", pendientes[0].Argumentos);
            Assert.Contains("--orden=segundo", pendientes[1].Argumentos);
        }

        [Fact]
        public async Task EncolarAsync_ConDatosJsonb_DebeGuardarCorrectamente()
        {
            var ruta = PrefijoTest + "encolar_jsonb";
            await PrepararTestAsync(ruta);

            var datosJson = @"{
                ""orderId"": 12345,
                ""items"": [{""sku"": ""ABC"", ""cantidad"": 2}],
                ""total"": 99.99
            }";

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                DatosDeComando = datosJson,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var datosDb = await connection.QuerySingleAsync<string>(
                $"SELECT datos_comando::text FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = id });

            Assert.Contains("12345", datosDb);
            Assert.Contains("ABC", datosDb);
        }
    }
}
