using Microsoft.EntityFrameworkCore;
using Npgsql;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest.Infraestructura;

public sealed class PostgreSqlPrueba : IAsyncDisposable
{
    private readonly string connectionStringBase;
    private readonly string esquema;

    private PostgreSqlPrueba(string connectionStringBase, string esquema)
    {
        this.connectionStringBase = connectionStringBase;
        this.esquema = esquema;

        NpgsqlConnectionStringBuilder builder = new(connectionStringBase)
        {
            SearchPath = esquema
        };

        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgreSqlPrueba> CrearAsync()
    {
        string connectionStringBase = LeerConnectionString();
        string esquema = $"test_mensajeria_{Guid.NewGuid():N}";
        PostgreSqlPrueba prueba = new(connectionStringBase, esquema);

        try
        {
            await EjecutarSqlAsync(connectionStringBase, $"CREATE SCHEMA \"{esquema}\";");
            await EjecutarSqlAsync(connectionStringBase, $"SET search_path TO \"{esquema}\";{Environment.NewLine}{LeerTablasSql()}");
            return prueba;
        }
        catch
        {
            await prueba.DisposeAsync();
            throw;
        }
    }

    public MensajeriaContextoDB CrearContexto()
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new MensajeriaContextoDB(opciones);
    }

    public async ValueTask DisposeAsync()
    {
        await EjecutarSqlAsync(connectionStringBase, $"DROP SCHEMA IF EXISTS \"{esquema}\" CASCADE;");
    }

    public async Task<DAOCuentaCanal> CrearCuentaCanalAsync(string cuenta)
    {
        await using MensajeriaContextoDB contexto = CrearContexto();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(canalActual => canalActual.Canal == "whatsapp");
        DAOCuentaCanal cuentaCanal = new()
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = $"Cuenta {cuenta}",
            Activa = true
        };

        contexto.CuentasCanal.Add(cuentaCanal);
        await contexto.SaveChangesAsync();

        return cuentaCanal;
    }

    public async Task<(DAOCuentaCanal Cuenta, DAOConversacion Conversacion, DAOLineaConversacion Linea)> CrearConversacionAsync(string cuenta)
    {
        await using MensajeriaContextoDB contexto = CrearContexto();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(canalActual => canalActual.Canal == "whatsapp");
        DateTime fecha = DateTime.Now;
        DAOCuentaCanal cuentaCanal = new()
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = $"Cuenta {cuenta}",
            Activa = true
        };

        contexto.CuentasCanal.Add(cuentaCanal);
        await contexto.SaveChangesAsync();

        DAOConversacion conversacion = new()
        {
            IDCuentaCanal = cuentaCanal.ID,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };

        contexto.Conversaciones.Add(conversacion);
        await contexto.SaveChangesAsync();

        DAOLineaConversacion linea = new()
        {
            IDConversacion = conversacion.ID,
            FechaInicio = fecha,
            FechaUltimaActividad = fecha,
            Activa = true
        };

        contexto.LineasConversacion.Add(linea);
        await contexto.SaveChangesAsync();

        return (cuentaCanal, conversacion, linea);
    }

    public async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearMensajeEntradaPendienteAsync()
    {
        (DAOCuentaCanal cuenta, DAOConversacion conversacion, DAOLineaConversacion linea) = await CrearConversacionAsync($"cuenta_{Guid.NewGuid():N}");
        await using MensajeriaContextoDB contexto = CrearContexto();
        DateTime fecha = DateTime.Now;
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = linea.ID,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            TelefonoOrigen = "3001234567",
            TelefonoDestino = "6011234567",
            Contenido = "hola",
            IdentificadorExternoMensaje = $"externo_{Guid.NewGuid():N}",
            FechaMensaje = fecha,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };

        contexto.Mensajes.Add(mensaje);
        await contexto.SaveChangesAsync();

        DAOProcesamientoInternoMensaje procesamiento = new()
        {
            IDMensaje = mensaje.ID,
            IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
            IDEstadoProcesamientoInternoMensaje = "pendiente",
            Intentos = 0,
            FechaCreacion = fecha
        };

        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        return (mensaje, procesamiento);
    }

    public async Task<(DAOConversacion Conversacion, DAOLineaConversacion Linea, DAOMensaje Mensaje, DAOEnvioMensaje Envio)> CrearEnvioPendienteAsync()
    {
        (DAOCuentaCanal cuenta, DAOConversacion conversacion, DAOLineaConversacion linea) = await CrearConversacionAsync($"cuenta_{Guid.NewGuid():N}");
        await using MensajeriaContextoDB contexto = CrearContexto();
        DateTime fecha = DateTime.Now;
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = linea.ID,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "salida",
            TelefonoOrigen = "6011234567",
            TelefonoDestino = "3001234567",
            Contenido = "respuesta",
            FechaMensaje = fecha,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };

        contexto.Mensajes.Add(mensaje);
        await contexto.SaveChangesAsync();

        DAOEnvioMensaje envio = new()
        {
            IDMensaje = mensaje.ID,
            IDEstadoEnvioMensaje = "pendiente",
            Intentos = 0,
            FechaCreacion = fecha
        };

        contexto.EnviosMensaje.Add(envio);
        await contexto.SaveChangesAsync();

        return (conversacion, linea, mensaje, envio);
    }

    private static string LeerConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL es obligatoria para los tests funcionales con PostgreSQL.");

        return connectionString!;
    }

    private static async Task EjecutarSqlAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static string LeerTablasSql()
    {
        DirectoryInfo? directorio = new(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            string ruta = Path.Combine(directorio.FullName, "Datos", "Sql", "tablas.sql");

            if (File.Exists(ruta))
            {
                return File.ReadAllText(ruta);
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException("No se encontro Datos/Sql/tablas.sql.");
    }
}
