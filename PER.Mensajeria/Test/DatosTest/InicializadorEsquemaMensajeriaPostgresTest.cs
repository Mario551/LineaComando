using Npgsql;
using PER.Mensajeria.Datos.Esquema;

namespace DatosTest;

public class InicializadorEsquemaMensajeriaPostgresTest
{
    [Fact]
    public async Task InicializarAsync_DebeCrearTablasIndicesYCatalogosBaseEnEsquemaIndicado()
    {
        string connectionString = LeerConnectionString();
        string esquema = $"test_mensajeria_init_{Guid.NewGuid():N}";
        NombresBaseDatosMensajeria nombres = NombresBaseDatosMensajeria.Postgres(esquema);

        try
        {
            InicializadorEsquemaMensajeriaPostgres inicializador = new(connectionString, esquema);
            await inicializador.InicializarAsync();
            await inicializador.InicializarAsync();

            int tablas = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @esquema AND table_name IN ('per_mensajes', 'per_lineas_conversacion', 'per_procesamientos_internos_mensaje', 'per_envios_mensaje');",
                comando => comando.Parameters.AddWithValue("esquema", esquema));

            int direcciones = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM {nombres.DireccionesMensaje} WHERE id IN ('entrada', 'salida');");

            int estadosProcesamiento = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM {nombres.EstadosProcesamientoInternoMensaje} WHERE id IN ('pendiente', 'en_proceso', 'procesado', 'error');");

            int indices = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = @esquema AND indexname IN ('ux_mensajes_idempotencia', 'ix_procesamientos_internos_mensaje_estado_fecha', 'ix_envios_mensaje_estado_fecha');",
                comando => comando.Parameters.AddWithValue("esquema", esquema));

            Assert.Equal(4, tablas);
            Assert.Equal(2, direcciones);
            Assert.Equal(4, estadosProcesamiento);
            Assert.Equal(3, indices);
        }
        finally
        {
            await EjecutarAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{esquema}\" CASCADE;");
        }
    }

    private static string LeerConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL es obligatoria para validar el inicializador PostgreSQL.");

        return connectionString!;
    }

    private static async Task<int> ConsultarEnteroAsync(string connectionString, string sql, Action<NpgsqlCommand>? configurar = null)
    {
        await using NpgsqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexion);
        configurar?.Invoke(comando);
        object? resultado = await comando.ExecuteScalarAsync();
        return Convert.ToInt32(resultado);
    }

    private static async Task EjecutarAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
