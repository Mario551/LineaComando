using Dapper;
using PER.Comandos.LineaComandos.EventDriven.DAO;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Registro;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace EventDrivenTest
{
    [Collection("DatabaseEventDriven")]
    public class RegistroManejadoresTest : BaseIntegracionTestEventDriven
    {
        private readonly RegistroManejadores _registroManejadores;
        private readonly RegistroTiposEvento _registroTipos;
        private readonly RegistroComandos<string, object> _registroComandos;

        protected override string PrefijoTest => "registro_manejadores_";

        public RegistroManejadoresTest(DatabaseFixtureEventDriven fixture) : base(fixture)
        {
            _registroManejadores = new RegistroManejadores(ConnectionString);
            _registroTipos = new RegistroTiposEvento(ConnectionString);
            _registroComandos = new RegistroComandos<string, object>(ConnectionString);
        }

        private async Task<int> CrearComandoRegistradoAsync(string rutaComando)
        {
            var metadatos = new MetadatosComando
            {
                RutaComando = rutaComando,
                Descripcion = "Comando de prueba"
            };

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var id = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO comandos_registrados (ruta_comando, descripcion, activo, creado_en)
                  VALUES (@RutaComando, @Descripcion, true, NOW())
                  ON CONFLICT (ruta_comando)
                  DO UPDATE SET descripcion = EXCLUDED.descripcion
                  RETURNING id;",
                metadatos);

            return id;
        }

        private async Task<int> CrearTipoEventoAsync(string codigo)
        {
            var tipo = new TipoEvento
            {
                Codigo = codigo,
                Nombre = $"Tipo {codigo}",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            return await _registroTipos.RegistrarTipoEventoAsync(tipo);
        }

        private async Task CrearDisparadorAsync(int manejadorId, int tipoEventoId, int prioridad = 0)
        {
            using var connection = CrearConexion();
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                @"INSERT INTO disparadores_manejador
                  (manejador_evento_id, modo_disparo, tipo_evento_id, activo, prioridad, creado_en)
                  VALUES (@ManejadorId, 'Evento', @TipoEventoId, true, @Prioridad, NOW());",
                new { ManejadorId = manejadorId, TipoEventoId = tipoEventoId, Prioridad = prioridad });
        }

        private async Task CrearDisparadorProgramadoAsync(int manejadorId, string expresionCron, int prioridad = 0)
        {
            using var connection = CrearConexion();
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                @"INSERT INTO disparadores_manejador
                  (manejador_evento_id, modo_disparo, expresion_cron, activo, prioridad, creado_en)
                  VALUES (@ManejadorId, 'Programado', @ExpresionCron, true, @Prioridad, NOW());",
                new { ManejadorId = manejadorId, ExpresionCron = expresionCron, Prioridad = prioridad });
        }

        [Fact]
        public async Task RegistrarManejadorAsync_DebeInsertarManejador()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_test");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_nuevo",
                Nombre = "Manejador Nuevo",
                Descripcion = "Descripcion del manejador",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_test",
                ArgumentosComando = "--param=valor",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registroManejadores.RegistrarManejadorAsync(manejador);

            Assert.True(id > 0);

            var manejadorDb = await _registroManejadores.ObtenerManejadorPorIdAsync(id);

            Assert.NotNull(manejadorDb);
            Assert.Equal(manejador.Codigo, manejadorDb.Codigo);
            Assert.Equal(manejador.Nombre, manejadorDb.Nombre);
            Assert.Equal(manejador.RutaComando, manejadorDb.RutaComando);
            Assert.Equal(manejador.ArgumentosComando, manejadorDb.ArgumentosComando);
        }

        [Fact]
        public async Task RegistrarManejadorAsync_CodigoDuplicado_DebeActualizar()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_upsert");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_upsert",
                Nombre = "Nombre Original",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_upsert",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id1 = await _registroManejadores.RegistrarManejadorAsync(manejador);

            manejador.Nombre = "Nombre Actualizado";
            manejador.Descripcion = "Nueva descripcion";

            var id2 = await _registroManejadores.RegistrarManejadorAsync(manejador);

            Assert.Equal(id1, id2);

            var manejadorDb = await _registroManejadores.ObtenerManejadorPorIdAsync(id1);

            Assert.NotNull(manejadorDb);
            Assert.Equal("Nombre Actualizado", manejadorDb.Nombre);
            Assert.Equal("Nueva descripcion", manejadorDb.Descripcion);
        }

        [Fact]
        public async Task ObtenerManejadorPorIdAsync_ManejadorExistente_DebeRetornar()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_buscar_id");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "buscar_por_id",
                Nombre = "Buscar Por Id",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_buscar_id",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registroManejadores.RegistrarManejadorAsync(manejador);

            var manejadorDb = await _registroManejadores.ObtenerManejadorPorIdAsync(id);

            Assert.NotNull(manejadorDb);
            Assert.Equal(id, manejadorDb.Id);
            Assert.Equal(manejador.Codigo, manejadorDb.Codigo);
        }

        [Fact]
        public async Task ObtenerManejadorPorIdAsync_ManejadorNoExistente_DebeRetornarNull()
        {
            var manejadorDb = await _registroManejadores.ObtenerManejadorPorIdAsync(999999);

            Assert.Null(manejadorDb);
        }

        [Fact]
        public async Task ObtenerManejadorPorCodigoAsync_ManejadorExistente_DebeRetornar()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_buscar_codigo");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "buscar_por_codigo",
                Nombre = "Buscar Por Codigo",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_buscar_codigo",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            await _registroManejadores.RegistrarManejadorAsync(manejador);

            var manejadorDb = await _registroManejadores.ObtenerManejadorPorCodigoAsync(manejador.Codigo);

            Assert.NotNull(manejadorDb);
            Assert.Equal(manejador.Codigo, manejadorDb.Codigo);
        }

        [Fact]
        public async Task ObtenerManejadorPorCodigoAsync_ManejadorNoExistente_DebeRetornarNull()
        {
            var manejadorDb = await _registroManejadores.ObtenerManejadorPorCodigoAsync(PrefijoTest + "no_existe");

            Assert.Null(manejadorDb);
        }

        [Fact]
        public async Task ObtenerManejadoresActivosAsync_DebeRetornarSoloActivos()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_activos");

            var manejadorActivo = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_activo",
                Nombre = "Activo",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_activos",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var manejadorInactivo = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_inactivo",
                Nombre = "Inactivo",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_activos",
                Activo = false,
                CreadoEn = DateTime.UtcNow
            };

            await _registroManejadores.RegistrarManejadorAsync(manejadorActivo);
            await _registroManejadores.RegistrarManejadorAsync(manejadorInactivo);

            var activos = await _registroManejadores.ObtenerManejadoresActivosAsync();

            var activosFiltrados = activos.Where(m => m.Codigo.StartsWith(PrefijoTest)).ToList();

            Assert.Single(activosFiltrados);
            Assert.Equal(manejadorActivo.Codigo, activosFiltrados.First().Codigo);
        }

        [Fact]
        public async Task ActualizarManejadorAsync_DebeModificarDatos()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_actualizar");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_actualizar",
                Nombre = "Nombre Original",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_actualizar",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registroManejadores.RegistrarManejadorAsync(manejador);

            manejador.Id = id;
            manejador.Nombre = "Nombre Modificado";
            manejador.ArgumentosComando = "--nuevo=parametro";

            await _registroManejadores.ActualizarManejadorAsync(manejador);

            var manejadorDb = await _registroManejadores.ObtenerManejadorPorIdAsync(id);

            Assert.NotNull(manejadorDb);
            Assert.Equal("Nombre Modificado", manejadorDb.Nombre);
            Assert.Equal("--nuevo=parametro", manejadorDb.ArgumentosComando);
        }

        [Fact]
        public async Task DesactivarManejadorAsync_DebeCambiarActivo()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_desactivar");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_desactivar",
                Nombre = "A Desactivar",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_desactivar",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registroManejadores.RegistrarManejadorAsync(manejador);

            await _registroManejadores.DesactivarManejadorAsync(id);

            var manejadorDb = await _registroManejadores.ObtenerManejadorPorIdAsync(id);

            Assert.NotNull(manejadorDb);
            Assert.False(manejadorDb.Activo);
        }

        [Fact]
        public async Task ObtenerManejadoresParaEventoAsync_DebeRetornarConfigurados()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_evento");
            var tipoEventoId = await CrearTipoEventoAsync(PrefijoTest + "tipo_evento_test");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_evento",
                Nombre = "Manejador Para Evento",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_evento",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var manejadorId = await _registroManejadores.RegistrarManejadorAsync(manejador);
            await CrearDisparadorAsync(manejadorId, tipoEventoId);

            var configuraciones = await _registroManejadores.ObtenerManejadoresParaEventoAsync(PrefijoTest + "tipo_evento_test");

            Assert.Single(configuraciones);
            var config = configuraciones.First();
            Assert.Equal(PrefijoTest + "comando_evento", config.RutaComando);
            Assert.Equal("Evento", config.ModoDisparo);
        }

        [Fact]
        public async Task ObtenerManejadoresParaEventoAsync_ConPrioridad_DebeOrdenarPorPrioridad()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_prioridad");
            var tipoEventoId = await CrearTipoEventoAsync(PrefijoTest + "tipo_prioridad");

            var manejador1 = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_prioridad_baja",
                Nombre = "Prioridad Baja",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_prioridad",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var manejador2 = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_prioridad_alta",
                Nombre = "Prioridad Alta",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_prioridad",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id1 = await _registroManejadores.RegistrarManejadorAsync(manejador1);
            var id2 = await _registroManejadores.RegistrarManejadorAsync(manejador2);

            await CrearDisparadorAsync(id1, tipoEventoId, prioridad: 10);
            await CrearDisparadorAsync(id2, tipoEventoId, prioridad: 1);

            var configuraciones = (await _registroManejadores.ObtenerManejadoresParaEventoAsync(PrefijoTest + "tipo_prioridad")).ToList();

            Assert.Equal(2, configuraciones.Count);
            Assert.Equal(1, configuraciones[0].Prioridad);
            Assert.Equal(10, configuraciones[1].Prioridad);
        }

        [Fact]
        public async Task ObtenerManejadoresProgramadosAsync_DebeRetornarProgramados()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_programado");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_programado",
                Nombre = "Manejador Programado",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_programado",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var manejadorId = await _registroManejadores.RegistrarManejadorAsync(manejador);
            await CrearDisparadorProgramadoAsync(manejadorId, "0 * * * *");

            var configuraciones = await _registroManejadores.ObtenerManejadoresProgramadosAsync();

            var configFiltradas = configuraciones.Where(c => c.RutaComando.StartsWith(PrefijoTest)).ToList();

            Assert.Single(configFiltradas);
            Assert.Equal("Programado", configFiltradas.First().ModoDisparo);
            Assert.Equal("0 * * * *", configFiltradas.First().ExpresionCron);
        }

        [Fact]
        public async Task ObtenerManejadoresParaEventoAsync_ManejadorInactivo_NoDebeRetornar()
        {
            var comandoId = await CrearComandoRegistradoAsync(PrefijoTest + "comando_inactivo");
            var tipoEventoId = await CrearTipoEventoAsync(PrefijoTest + "tipo_inactivo_test");

            var manejador = new ManejadorEvento
            {
                Codigo = PrefijoTest + "manejador_inactivo_test",
                Nombre = "Manejador Inactivo",
                IdComandoRegistrado = comandoId,
                RutaComando = PrefijoTest + "comando_inactivo",
                Activo = false,
                CreadoEn = DateTime.UtcNow
            };

            var manejadorId = await _registroManejadores.RegistrarManejadorAsync(manejador);
            await CrearDisparadorAsync(manejadorId, tipoEventoId);

            var configuraciones = await _registroManejadores.ObtenerManejadoresParaEventoAsync(PrefijoTest + "tipo_inactivo_test");

            Assert.Empty(configuraciones);
        }

        [Fact]
        public void Constructor_ConnectionStringNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() => new RegistroManejadores(null!));
        }
    }
}
