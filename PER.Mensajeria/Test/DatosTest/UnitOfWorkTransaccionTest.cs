using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using PER.Mensajeria.Datos.Configuracion;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace DatosTest;

public class UnitOfWorkTransaccionTest
{
    public static IEnumerable<object[]> Motores
    {
        get
        {
            yield return new object[] { MotorBaseDatosTransaccionPrueba.PostgreSql };
            yield return new object[] { MotorBaseDatosTransaccionPrueba.SqlServer };
        }
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task BeginTransactionAsync_ConTransaccionActiva_DebeFallar(MotorBaseDatosTransaccionPrueba motor)
    {
        BaseDatosTransaccionPrueba baseDatos = await CrearBaseDePruebaAsync(motor);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);

        await unitOfWork.BeginTransactionAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.BeginTransactionAsync(CancellationToken.None));

        await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task CommitTransactionAsync_DebePersistirCambios(MotorBaseDatosTransaccionPrueba motor)
    {
        BaseDatosTransaccionPrueba baseDatos = await CrearBaseDePruebaAsync(motor);
        string cuenta = $"commit_{Guid.NewGuid():N}";

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(canalActual => canalActual.Canal == "whatsapp");
        UnitOfWork unitOfWork = new(contexto);
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);

        DAOCuentaCanal cuentaCanal = new()
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = "Cuenta commit",
            Activa = true
        };

        contexto.CuentasCanal.Add(cuentaCanal);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        DAOConversacion conversacion = new()
        {
            IDCuentaCanal = cuentaCanal.ID,
            FechaCreacion = DateTime.Now,
            FechaActualizacion = DateTime.Now
        };

        await unitOfWork.ConversacionRepositorio.AgregarAsync(conversacion, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        await unitOfWork.CommitTransactionAsync(CancellationToken.None);

        await using MensajeriaContextoDB contextoVerificacion = baseDatos.CrearContexto();
        bool existe = await contextoVerificacion.Conversaciones.AnyAsync(
            conversacionActual => conversacionActual.ID == conversacion.ID,
            CancellationToken.None);

        Assert.True(existe);
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task RollbackTransactionAsync_DebeRevertirCambios(MotorBaseDatosTransaccionPrueba motor)
    {
        BaseDatosTransaccionPrueba baseDatos = await CrearBaseDePruebaAsync(motor);
        string cuenta = $"rollback_{Guid.NewGuid():N}";

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(canalActual => canalActual.Canal == "whatsapp");
        UnitOfWork unitOfWork = new(contexto);
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);

        DAOCuentaCanal cuentaCanal = new()
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = "Cuenta rollback",
            Activa = true
        };

        contexto.CuentasCanal.Add(cuentaCanal);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        await unitOfWork.RollbackTransactionAsync(CancellationToken.None);

        await using MensajeriaContextoDB contextoVerificacion = baseDatos.CrearContexto();
        bool existe = await contextoVerificacion.CuentasCanal.AnyAsync(
            cuentaActual => cuentaActual.Cuenta == cuenta,
            CancellationToken.None);

        Assert.False(existe);
    }

    private static Task<BaseDatosTransaccionPrueba> CrearBaseDePruebaAsync(MotorBaseDatosTransaccionPrueba motor)
    {
        return motor switch
        {
            MotorBaseDatosTransaccionPrueba.PostgreSql => CrearPostgreSqlPruebaAsync(),
            MotorBaseDatosTransaccionPrueba.SqlServer => CrearSqlServerPruebaAsync(),
            _ => throw new NotSupportedException($"Motor de base de datos no soportado: {motor}.")
        };
    }

    private static async Task<BaseDatosTransaccionPrueba> CrearPostgreSqlPruebaAsync()
    {
        string connectionString = LeerConnectionStringPostgreSql();
        string esquema = $"test_mensajeria_{Guid.NewGuid():N}";
        InicializadorEsquemaMensajeriaPostgres inicializador = new(connectionString, esquema);
        await inicializador.InicializarAsync();

        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            SearchPath = esquema
        };

        return new BaseDatosTransaccionPrueba(
            MotorBaseDatosTransaccionPrueba.PostgreSql,
            builder.ConnectionString,
            esquema);
    }

    private static async Task<BaseDatosTransaccionPrueba> CrearSqlServerPruebaAsync()
    {
        string connectionString = LeerConnectionStringSqlServer();
        string esquema = $"test_mensajeria_sql_{Guid.NewGuid():N}";
        InicializadorEsquemaMensajeriaSqlServer inicializador = new(connectionString, esquema);
        await inicializador.InicializarAsync();

        return new BaseDatosTransaccionPrueba(
            MotorBaseDatosTransaccionPrueba.SqlServer,
            connectionString,
            esquema);
    }

    private static string LeerConnectionStringPostgreSql()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL es obligatoria para los tests funcionales con PostgreSQL.");

        return connectionString!;
    }

    private static string LeerConnectionStringSqlServer()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MENSAJERIA_COMANDOS_CONEXION_SQLSERVER");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_SQLSERVER es obligatoria para los tests funcionales con SQL Server.");

        return connectionString!;
    }

    public enum MotorBaseDatosTransaccionPrueba
    {
        PostgreSql,
        SqlServer
    }

    private sealed class BaseDatosTransaccionPrueba
    {
        public BaseDatosTransaccionPrueba(MotorBaseDatosTransaccionPrueba motor, string connectionString, string esquema)
        {
            Motor = motor;
            ConnectionString = connectionString;
            Esquema = esquema;
        }

        public MotorBaseDatosTransaccionPrueba Motor { get; }

        public string ConnectionString { get; }

        public string Esquema { get; }

        public MensajeriaContextoDB CrearContexto()
        {
            DbContextOptionsBuilder<MensajeriaContextoDB> builder = new();
            builder.ReplaceService<IModelCacheKeyFactory, ModeloCachePorContextoTransaccionPrueba>();

            if (Motor == MotorBaseDatosTransaccionPrueba.PostgreSql)
            {
                builder.UseNpgsql(ConnectionString);
                return new MensajeriaContextoDB(builder.Options);
            }

            builder.UseSqlServer(ConnectionString);
            return new MensajeriaContextoDB(
                builder.Options,
                new ConfiguracionMensajeriaContextoDB { Esquema = Esquema });
        }
    }

    private sealed class ModeloCachePorContextoTransaccionPrueba : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            return (context.GetType(), context.ContextId.InstanceId, designTime);
        }
    }
}
