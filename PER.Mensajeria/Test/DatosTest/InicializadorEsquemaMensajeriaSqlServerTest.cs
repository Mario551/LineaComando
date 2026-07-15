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
        "per_roles_contexto_ia",
        "per_tipos_entrada_contexto_ia",
        "per_cuentas_canal",
        "per_participantes_conversacion",
        "per_conversaciones",
        "per_conversaciones_participantes",
        "per_lineas_conversacion",
        "per_mensajes",
        "per_archivos_mensaje",
        "per_procesamientos_internos_mensaje",
        "per_metadata_razonamiento_ia_linea_conversacion",
        "per_entradas_contexto_ia",
        "per_estados_contexto_conversacion",
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
        await EjecutarAsync(
            connectionString,
            $"UPDATE {nombres.DireccionesMensaje} SET descripcion = 'Entrada personalizada' WHERE id = 'entrada';");
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

            int rolesContextoIA = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM {nombres.RolesContextoIA} WHERE id IN ('system', 'user', 'assistant', 'tool');");

            int tiposEntradaContextoIA = await ConsultarEnteroAsync(
                connectionString,
                $"SELECT COUNT(*) FROM {nombres.TiposEntradaContextoIA} WHERE id IN ('mensaje_entrada', 'decision_comando', 'decision_historial', 'respuesta_final', 'no_responder', 'error_intencion', 'resultado_comando', 'resultado_historial', 'limite_ventana');");

            int indices = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id IN (OBJECT_ID(@mensajes), OBJECT_ID(@procesamientos), OBJECT_ID(@envios), OBJECT_ID(@entradas), OBJECT_ID(@metadata), OBJECT_ID(@lineas), OBJECT_ID(@estadosContexto)) AND name IN ('ux_mensajes_idempotencia', 'ix_procesamientos_internos_mensaje_estado_fecha', 'ix_envios_mensaje_estado_fecha', 'ix_entradas_contexto_ia_linea_orden', 'ix_entradas_contexto_ia_procesamiento_orden', 'ix_metadata_ia_linea_iteracion', 'ix_metadata_ia_procesamiento_iteracion', 'ux_lineas_conversacion_estado_contexto_inicial', 'ux_estados_contexto_linea_origen', 'ux_estados_contexto_conversacion_version', 'ix_estados_contexto_anterior', 'ux_estados_contexto_metadata');",
                comando =>
                {
                    comando.Parameters.AddWithValue("mensajes", $"{esquema}.per_mensajes");
                    comando.Parameters.AddWithValue("procesamientos", $"{esquema}.per_procesamientos_internos_mensaje");
                    comando.Parameters.AddWithValue("envios", $"{esquema}.per_envios_mensaje");
                    comando.Parameters.AddWithValue("entradas", $"{esquema}.per_entradas_contexto_ia");
                    comando.Parameters.AddWithValue("metadata", $"{esquema}.per_metadata_razonamiento_ia_linea_conversacion");
                    comando.Parameters.AddWithValue("lineas", $"{esquema}.per_lineas_conversacion");
                    comando.Parameters.AddWithValue("estadosContexto", $"{esquema}.per_estados_contexto_conversacion");
                });

            int llavesForaneasContextoIA = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN (OBJECT_ID(@entradas), OBJECT_ID(@metadata)) AND name IN ('fk_entradas_contexto_ia_linea', 'fk_entradas_contexto_ia_mensaje', 'fk_entradas_contexto_ia_procesamiento', 'fk_entradas_contexto_ia_metadata', 'fk_entradas_contexto_ia_rol', 'fk_entradas_contexto_ia_tipo', 'fk_metadata_ia_linea', 'fk_metadata_ia_procesamiento', 'fk_metadata_ia_mensaje');",
                comando =>
                {
                    comando.Parameters.AddWithValue("entradas", $"{esquema}.per_entradas_contexto_ia");
                    comando.Parameters.AddWithValue("metadata", $"{esquema}.per_metadata_razonamiento_ia_linea_conversacion");
                });

            int llavesForaneasSnapshot = await ConsultarEnteroAsync(
                connectionString,
                "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN (OBJECT_ID(@lineas), OBJECT_ID(@estadosContexto)) AND name IN ('fk_lineas_conversacion_estado_contexto_inicial', 'fk_estados_contexto_conversacion', 'fk_estados_contexto_linea_origen', 'fk_estados_contexto_anterior', 'fk_estados_contexto_metadata');",
                comando =>
                {
                    comando.Parameters.AddWithValue("lineas", $"{esquema}.per_lineas_conversacion");
                    comando.Parameters.AddWithValue("estadosContexto", $"{esquema}.per_estados_contexto_conversacion");
                });

            int totalDirecciones = await ConsultarEnteroAsync(connectionString, $"SELECT COUNT(*) FROM {nombres.DireccionesMensaje};");
            int totalEstadosProcesamiento = await ConsultarEnteroAsync(connectionString, $"SELECT COUNT(*) FROM {nombres.EstadosProcesamientoInternoMensaje};");
            string descripcionEntrada = await ConsultarTextoAsync(
                connectionString,
                $"SELECT descripcion FROM {nombres.DireccionesMensaje} WHERE id = 'entrada';");

            Assert.Equal(1, esquemaCreado);
            Assert.Equal(TablasEsperadas.Length, tablas);
            Assert.Equal(2, direcciones);
            Assert.Equal(4, estadosProcesamiento);
            Assert.Equal(4, rolesContextoIA);
            Assert.Equal(9, tiposEntradaContextoIA);
            Assert.Equal(12, indices);
            Assert.Equal(9, llavesForaneasContextoIA);
            Assert.Equal(5, llavesForaneasSnapshot);
            Assert.Equal(2, totalDirecciones);
            Assert.Equal(4, totalEstadosProcesamiento);
            Assert.Equal("Entrada personalizada", descripcionEntrada);
    }

    [Fact]
    public async Task InicializarAsync_EstructuraParcial_DebeFallarIndicandoObjetosFaltantes()
    {
        string connectionString = LeerConnectionString();
        string esquema = $"test_mensajeria_sql_parcial_{Guid.NewGuid():N}";
        NombresBaseDatosMensajeria nombres = NombresBaseDatosMensajeria.SqlServer(esquema);
        await EjecutarAsync(connectionString, $"CREATE SCHEMA {nombres.EsquemaSql};");
        await EjecutarAsync(
            connectionString,
            $"CREATE TABLE {nombres.CanalesComunicacion} (id INT IDENTITY(1,1) PRIMARY KEY, canal NVARCHAR(64) NOT NULL, descripcion NVARCHAR(MAX) NOT NULL);");
        InicializadorEsquemaMensajeriaSqlServer inicializador = new(connectionString, esquema);

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => inicializador.InicializarAsync());

        Assert.Contains("esta incompleto", excepcion.Message);
        Assert.Contains("per_mensajes", excepcion.Message);
        Assert.Contains("per_estados_contexto_conversacion", excepcion.Message);
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

    private static async Task<string> ConsultarTextoAsync(string connectionString, string sql)
    {
        await using SqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using SqlCommand comando = new(sql, conexion);
        object? resultado = await comando.ExecuteScalarAsync();
        return Convert.ToString(resultado) ?? string.Empty;
    }

    private static async Task EjecutarAsync(string connectionString, string sql)
    {
        await using SqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using SqlCommand comando = new(sql, conexion);
        await comando.ExecuteNonQueryAsync();
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
