using Microsoft.Data.SqlClient;
using PER.Mensajeria.Datos.Esquema;

namespace DatosTest;

public class InicializadorEsquemaMensajeriaSqlServerTest
{
    private static readonly string[] TablasEsperadas =
    [
        "per_canales_comunicacion",
        "per_tipos_participante_conversacion",
        "per_tipos_mensaje",
        "per_direcciones_mensaje",
        "per_tipos_contenido_archivo",
        "per_tipos_procesamiento_interno_mensaje",
        "per_estados_procesamiento_interno_mensaje",
        "per_estados_envio_mensaje",
        "per_cuentas_canal",
        "per_participantes_conversacion",
        "per_conversaciones",
        "per_conversaciones_participantes",
        "per_lineas_conversacion",
        "per_mensajes",
        "per_archivos_mensaje",
        "per_procesamientos_internos_mensaje",
        "per_envios_mensaje"
    ];

    [Fact]
    public async Task InicializarAsync_DebeCrearTablasIndicesYCatalogosBaseEnEsquemaIndicado()
    {
        string connectionString = LeerConnectionString();
        string esquema = $"test_mensajeria_sql_{Guid.NewGuid():N}";
        NombresBaseDatosMensajeria nombres = NombresBaseDatosMensajeria.SqlServer(esquema);

        InicializadorEsquemaMensajeriaSqlServer inicializador = new(connectionString, esquema);
        await inicializador.InicializarAsync();
        await inicializador.InicializarAsync();

            int esquemaCreado = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM sys.schemas WHERE name = @esquema;",
                comando => comando.Parameters.AddWithValue("esquema", esquema));

            int tablas = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(@esquema) AND name IN ({CrearParametrosIn(TablasEsperadas, "tabla")});",
                comando =>
                {
                    comando.Parameters.AddWithValue("esquema", esquema);
                    AgregarParametros(comando, "tabla", TablasEsperadas);
                });

            int direcciones = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM {nombres.DireccionesMensaje} WHERE id IN ('entrada', 'salida');");

            int estadosProcesamiento = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM {nombres.EstadosProcesamientoInternoMensaje} WHERE id IN ('pendiente', 'en_proceso', 'procesado', 'error');");

            int indices = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id IN (OBJECT_ID(@mensajes), OBJECT_ID(@procesamientos), OBJECT_ID(@envios)) AND name IN ('ux_mensajes_idempotencia', 'ix_procesamientos_internos_mensaje_estado_fecha', 'ix_envios_mensaje_estado_fecha');",
                comando =>
                {
                    comando.Parameters.AddWithValue("mensajes", $"{esquema}.per_mensajes");
                    comando.Parameters.AddWithValue("procesamientos", $"{esquema}.per_procesamientos_internos_mensaje");
                    comando.Parameters.AddWithValue("envios", $"{esquema}.per_envios_mensaje");
                });

            int totalDirecciones = await ConsultarEnteroAsync(connectionString, $"SELECT COUNT(*) FROM {nombres.DireccionesMensaje};");
            int totalEstadosProcesamiento = await ConsultarEnteroAsync(connectionString, $"SELECT COUNT(*) FROM {nombres.EstadosProcesamientoInternoMensaje};");

            Assert.Equal(1, esquemaCreado);
            Assert.Equal(TablasEsperadas.Length, tablas);
            Assert.Equal(2, direcciones);
            Assert.Equal(4, estadosProcesamiento);
            Assert.Equal(3, indices);
            Assert.Equal(2, totalDirecciones);
            Assert.Equal(4, totalEstadosProcesamiento);
    }

    private static string LeerConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_SQLSERVER");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_SQLSERVER es obligatoria para validar el inicializador SQL Server.");

        return connectionString!;
    }

    private static async Task<int> ConsultarEnteroAsync(string connectionString, string sql, Action<SqlCommand>? configurar = null)
    {
        await using SqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using SqlCommand comando = new(sql, conexion);
        configurar?.Invoke(comando);
        object? resultado = await comando.ExecuteScalarAsync();
        return Convert.ToInt32(resultado);
    }

    private static string CrearParametrosIn(IReadOnlyCollection<string> valores, string prefijo)
    {
        return string.Join(", ", Enumerable.Range(0, valores.Count).Select(indice => $"@{prefijo}{indice}"));
    }

    private static void AgregarParametros(SqlCommand comando, string prefijo, IReadOnlyList<string> valores)
    {
        for (int indice = 0; indice < valores.Count; indice++)
        {
            comando.Parameters.AddWithValue($"{prefijo}{indice}", valores[indice]);
        }
    }
}
