using Microsoft.EntityFrameworkCore;
using Npgsql;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace DatosTest;

public class UnitOfWorkTransaccionTest
{
    [Fact]
    public async Task BeginTransactionAsync_ConTransaccionActiva_DebeFallar()
    {
        string connectionString = LeerConnectionString();

        string esquema = CrearNombreEsquema();
        string connectionStringEsquema = await CrearBaseDePruebaAsync(connectionString, esquema);

        try
        {
            await using MensajeriaContextoDB contexto = CrearContexto(connectionStringEsquema);
            UnitOfWork unitOfWork = new(contexto);

            await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.BeginTransactionAsync(CancellationToken.None));

            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
        finally
        {
            await EliminarEsquemaAsync(connectionString, esquema);
        }
    }

    [Fact]
    public async Task CommitTransactionAsync_DebePersistirCambios()
    {
        string connectionString = LeerConnectionString();

        string esquema = CrearNombreEsquema();
        string connectionStringEsquema = await CrearBaseDePruebaAsync(connectionString, esquema);
        string cuenta = $"commit_{Guid.NewGuid():N}";

        try
        {
            await using MensajeriaContextoDB contexto = CrearContexto(connectionStringEsquema);
            UnitOfWork unitOfWork = new(contexto);
            await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            DAOCuentaCanal cuentaCanal = new()
            {
                IDCanalComunicacion = 1,
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

            await using MensajeriaContextoDB contextoVerificacion = CrearContexto(connectionStringEsquema);
            bool existe = await contextoVerificacion.Conversaciones.AnyAsync(
                conversacionActual => conversacionActual.ID == conversacion.ID,
                CancellationToken.None);

            Assert.True(existe);
        }
        finally
        {
            await EliminarEsquemaAsync(connectionString, esquema);
        }
    }

    [Fact]
    public async Task RollbackTransactionAsync_DebeRevertirCambios()
    {
        string connectionString = LeerConnectionString();

        string esquema = CrearNombreEsquema();
        string connectionStringEsquema = await CrearBaseDePruebaAsync(connectionString, esquema);
        string cuenta = $"rollback_{Guid.NewGuid():N}";

        try
        {
            await using MensajeriaContextoDB contexto = CrearContexto(connectionStringEsquema);
            UnitOfWork unitOfWork = new(contexto);
            await unitOfWork.BeginTransactionAsync(CancellationToken.None);

            DAOCuentaCanal cuentaCanal = new()
            {
                IDCanalComunicacion = 1,
                Cuenta = cuenta,
                Descripcion = "Cuenta rollback",
                Activa = true
            };

            contexto.CuentasCanal.Add(cuentaCanal);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);

            await using MensajeriaContextoDB contextoVerificacion = CrearContexto(connectionStringEsquema);
            bool existe = await contextoVerificacion.CuentasCanal.AnyAsync(
                cuentaActual => cuentaActual.Cuenta == cuenta,
                CancellationToken.None);

            Assert.False(existe);
        }
        finally
        {
            await EliminarEsquemaAsync(connectionString, esquema);
        }
    }

    private static MensajeriaContextoDB CrearContexto(string connectionString)
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseNpgsql(connectionString)
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

    private static async Task<string> CrearBaseDePruebaAsync(string connectionString, string esquema)
    {
        await EjecutarSqlAsync(connectionString, $"CREATE SCHEMA \"{esquema}\";");
        await EjecutarSqlAsync(connectionString, $"SET search_path TO \"{esquema}\";{Environment.NewLine}{LeerTablasSql()}");

        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            SearchPath = esquema
        };

        return builder.ConnectionString;
    }

    private static async Task EliminarEsquemaAsync(string connectionString, string esquema)
    {
        await EjecutarSqlAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{esquema}\" CASCADE;");
    }

    private static async Task EjecutarSqlAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection conexion = new(connectionString);
        await conexion.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static string CrearNombreEsquema()
    {
        return $"test_mensajeria_{Guid.NewGuid():N}";
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
