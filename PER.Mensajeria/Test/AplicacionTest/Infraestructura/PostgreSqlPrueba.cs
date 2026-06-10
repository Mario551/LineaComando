using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using PER.Mensajeria.Datos.Configuracion;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest.Infraestructura;

public enum MotorBaseDatosPrueba
{
    PostgreSql,
    SqlServer
}

public abstract class BaseDatosPrueba : IAsyncDisposable
{
    protected BaseDatosPrueba(string connectionString, string esquema, MotorBaseDatosPrueba motor)
    {
        ConnectionString = connectionString;
        Esquema = esquema;
        Motor = motor;
    }

    public string ConnectionString { get; }

    public string Esquema { get; }

    public MotorBaseDatosPrueba Motor { get; }

    public static IEnumerable<object[]> Motores
    {
        get
        {
            yield return new object[] { MotorBaseDatosPrueba.PostgreSql };
            yield return new object[] { MotorBaseDatosPrueba.SqlServer };
        }
    }

    public static async Task<BaseDatosPrueba> CrearAsync(MotorBaseDatosPrueba motor)
    {
        return motor switch
        {
            MotorBaseDatosPrueba.PostgreSql => await PostgreSqlPrueba.CrearAsync(),
            MotorBaseDatosPrueba.SqlServer => await SqlServerPrueba.CrearAsync(),
            _ => throw new NotSupportedException($"Motor de base de datos no soportado: {motor}.")
        };
    }

    public abstract MensajeriaContextoDB CrearContexto();

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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
}

public sealed class PostgreSqlPrueba : BaseDatosPrueba
{
    private PostgreSqlPrueba(string connectionString, string esquema)
        : base(connectionString, esquema, MotorBaseDatosPrueba.PostgreSql)
    {
    }

    public static async Task<PostgreSqlPrueba> CrearAsync()
    {
        string connectionStringBase = LeerConnectionString();
        string esquema = $"test_mensajeria_{Guid.NewGuid():N}";
        InicializadorEsquemaMensajeriaPostgres inicializador = new(connectionStringBase, esquema);
        await inicializador.InicializarAsync();

        NpgsqlConnectionStringBuilder builder = new(connectionStringBase)
        {
            SearchPath = esquema
        };

        return new PostgreSqlPrueba(builder.ConnectionString, esquema);
    }

    public override MensajeriaContextoDB CrearContexto()
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseNpgsql(ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, ModeloCachePorContextoPrueba>()
            .Options;

        return new MensajeriaContextoDB(opciones);
    }

    private static string LeerConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL es obligatoria para los tests funcionales con PostgreSQL.");

        return connectionString!;
    }
}

public sealed class SqlServerPrueba : BaseDatosPrueba
{
    private SqlServerPrueba(string connectionString, string esquema)
        : base(connectionString, esquema, MotorBaseDatosPrueba.SqlServer)
    {
    }

    public static async Task<SqlServerPrueba> CrearAsync()
    {
        string connectionString = LeerConnectionString();
        string esquema = $"test_mensajeria_sql_{Guid.NewGuid():N}";
        InicializadorEsquemaMensajeriaSqlServer inicializador = new(connectionString, esquema);
        await inicializador.InicializarAsync();

        return new SqlServerPrueba(connectionString, esquema);
    }

    public override MensajeriaContextoDB CrearContexto()
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseSqlServer(ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, ModeloCachePorContextoPrueba>()
            .Options;

        return new MensajeriaContextoDB(opciones, new ConfiguracionMensajeriaContextoDB { Esquema = Esquema });
    }

    private static string LeerConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_SQLSERVER");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_SQLSERVER es obligatoria para los tests funcionales con SQL Server.");

        return connectionString!;
    }
}


public sealed class ModeloCachePorContextoPrueba : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        return (context.GetType(), context.ContextId.InstanceId, designTime);
    }
}
