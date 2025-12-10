using Dapper;
using PER.Comandos.LineaComandos.EventDriven.DAO;
using PER.Comandos.LineaComandos.EventDriven.Registro;

namespace EventDrivenTest
{
    [Collection("DatabaseEventDriven")]
    public class RegistroTiposEventoTest : BaseIntegracionTestEventDriven
    {
        private readonly RegistroTiposEvento _registro;

        protected override string PrefijoTest => "registro_tipos_";

        public RegistroTiposEventoTest(DatabaseFixtureEventDriven fixture) : base(fixture)
        {
            _registro = new RegistroTiposEvento(ConnectionString);
        }

        [Fact]
        public async Task RegistrarTipoEventoAsync_DebeInsertarTipo()
        {
            var tipoEvento = new TipoEvento
            {
                Codigo = PrefijoTest + "pedido_creado",
                Nombre = "Pedido Creado",
                Descripcion = "Se dispara cuando se crea un nuevo pedido",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registro.RegistrarTipoEventoAsync(tipoEvento);

            Assert.True(id > 0);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var tipoDb = await connection.QuerySingleAsync<TipoEvento>(
                @"SELECT id as Id, codigo as Codigo, nombre as Nombre,
                  descripcion as Descripcion, activo as Activo, creado_en as CreadoEn
                  FROM tipos_evento WHERE id = @Id",
                new { Id = id });

            Assert.Equal(tipoEvento.Codigo, tipoDb.Codigo);
            Assert.Equal(tipoEvento.Nombre, tipoDb.Nombre);
            Assert.Equal(tipoEvento.Descripcion, tipoDb.Descripcion);
            Assert.True(tipoDb.Activo);
        }

        [Fact]
        public async Task RegistrarTipoEventoAsync_CodigoDuplicado_DebeActualizar()
        {
            var tipoEvento = new TipoEvento
            {
                Codigo = PrefijoTest + "evento_upsert",
                Nombre = "Nombre Original",
                Descripcion = "Descripcion Original",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id1 = await _registro.RegistrarTipoEventoAsync(tipoEvento);

            tipoEvento.Nombre = "Nombre Actualizado";
            tipoEvento.Descripcion = "Descripcion Actualizada";

            var id2 = await _registro.RegistrarTipoEventoAsync(tipoEvento);

            Assert.Equal(id1, id2);

            var tipoDb = await _registro.ObtenerTipoEventoPorIdAsync(id1);

            Assert.NotNull(tipoDb);
            Assert.Equal("Nombre Actualizado", tipoDb.Nombre);
            Assert.Equal("Descripcion Actualizada", tipoDb.Descripcion);
        }

        [Fact]
        public async Task ObtenerTipoEventoPorCodigoAsync_TipoExistente_DebeRetornar()
        {
            var tipoEvento = new TipoEvento
            {
                Codigo = PrefijoTest + "buscar_codigo",
                Nombre = "Buscar Por Codigo",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            await _registro.RegistrarTipoEventoAsync(tipoEvento);

            var tipoDb = await _registro.ObtenerTipoEventoPorCodigoAsync(tipoEvento.Codigo);

            Assert.NotNull(tipoDb);
            Assert.Equal(tipoEvento.Codigo, tipoDb.Codigo);
            Assert.Equal(tipoEvento.Nombre, tipoDb.Nombre);
        }

        [Fact]
        public async Task ObtenerTipoEventoPorCodigoAsync_TipoNoExistente_DebeRetornarNull()
        {
            var tipoDb = await _registro.ObtenerTipoEventoPorCodigoAsync(PrefijoTest + "no_existe");

            Assert.Null(tipoDb);
        }

        [Fact]
        public async Task ObtenerTipoEventoPorIdAsync_TipoExistente_DebeRetornar()
        {
            var tipoEvento = new TipoEvento
            {
                Codigo = PrefijoTest + "buscar_id",
                Nombre = "Buscar Por Id",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registro.RegistrarTipoEventoAsync(tipoEvento);

            var tipoDb = await _registro.ObtenerTipoEventoPorIdAsync(id);

            Assert.NotNull(tipoDb);
            Assert.Equal(id, tipoDb.Id);
            Assert.Equal(tipoEvento.Codigo, tipoDb.Codigo);
        }

        [Fact]
        public async Task ObtenerTipoEventoPorIdAsync_TipoNoExistente_DebeRetornarNull()
        {
            var tipoDb = await _registro.ObtenerTipoEventoPorIdAsync(999999);

            Assert.Null(tipoDb);
        }

        [Fact]
        public async Task ObtenerTiposEventosActivosAsync_DebeRetornarSoloActivos()
        {
            var tipoActivo = new TipoEvento
            {
                Codigo = PrefijoTest + "tipo_activo",
                Nombre = "Tipo Activo",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var tipoInactivo = new TipoEvento
            {
                Codigo = PrefijoTest + "tipo_inactivo",
                Nombre = "Tipo Inactivo",
                Activo = false,
                CreadoEn = DateTime.UtcNow
            };

            await _registro.RegistrarTipoEventoAsync(tipoActivo);
            await _registro.RegistrarTipoEventoAsync(tipoInactivo);

            var activos = await _registro.ObtenerTiposEventosActivosAsync();

            var activosFiltrados = activos.Where(t => t.Codigo.StartsWith(PrefijoTest)).ToList();

            Assert.Single(activosFiltrados);
            Assert.Equal(tipoActivo.Codigo, activosFiltrados.First().Codigo);
        }

        [Fact]
        public async Task ActualizarTipoEventoAsync_DebeModificarDatos()
        {
            var tipoEvento = new TipoEvento
            {
                Codigo = PrefijoTest + "tipo_actualizar",
                Nombre = "Nombre Original",
                Descripcion = "Descripcion Original",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registro.RegistrarTipoEventoAsync(tipoEvento);

            tipoEvento.Id = id;
            tipoEvento.Nombre = "Nombre Modificado";
            tipoEvento.Descripcion = "Descripcion Modificada";

            await _registro.ActualizarTipoEventoAsync(tipoEvento);

            var tipoDb = await _registro.ObtenerTipoEventoPorIdAsync(id);

            Assert.NotNull(tipoDb);
            Assert.Equal("Nombre Modificado", tipoDb.Nombre);
            Assert.Equal("Descripcion Modificada", tipoDb.Descripcion);
        }

        [Fact]
        public async Task DesactivarTipoEventoAsync_DebeCambiarActivo()
        {
            var tipoEvento = new TipoEvento
            {
                Codigo = PrefijoTest + "tipo_desactivar",
                Nombre = "Tipo a Desactivar",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            var id = await _registro.RegistrarTipoEventoAsync(tipoEvento);

            await _registro.DesactivarTipoEventoAsync(id);

            var tipoDb = await _registro.ObtenerTipoEventoPorIdAsync(id);

            Assert.NotNull(tipoDb);
            Assert.False(tipoDb.Activo);
        }

        [Fact]
        public async Task ObtenerTiposEventosActivosAsync_DebeOrdenarPorCodigo()
        {
            var tipos = new[]
            {
                new TipoEvento { Codigo = PrefijoTest + "z_ultimo", Nombre = "Z", Activo = true, CreadoEn = DateTime.UtcNow },
                new TipoEvento { Codigo = PrefijoTest + "a_primero", Nombre = "A", Activo = true, CreadoEn = DateTime.UtcNow },
                new TipoEvento { Codigo = PrefijoTest + "m_medio", Nombre = "M", Activo = true, CreadoEn = DateTime.UtcNow }
            };

            foreach (var tipo in tipos)
            {
                await _registro.RegistrarTipoEventoAsync(tipo);
            }

            var activos = await _registro.ObtenerTiposEventosActivosAsync();
            var activosFiltrados = activos.Where(t => t.Codigo.StartsWith(PrefijoTest)).ToList();

            Assert.Equal(3, activosFiltrados.Count);
            Assert.Equal(PrefijoTest + "a_primero", activosFiltrados[0].Codigo);
            Assert.Equal(PrefijoTest + "m_medio", activosFiltrados[1].Codigo);
            Assert.Equal(PrefijoTest + "z_ultimo", activosFiltrados[2].Codigo);
        }

        [Fact]
        public void Constructor_ConnectionStringNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() => new RegistroTiposEvento(null!));
        }
    }
}
