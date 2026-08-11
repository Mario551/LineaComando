using AplicacionTest.Infraestructura;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Datos.Infobip.Esquema;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace AplicacionTest;

public class InicializadorModuloEsquemaInfobipTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task Inicializar_DosVeces_DebeCrearModuloYNoSobrescribirCatalogos(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        InicializadorModuloEsquemaInfobip inicializador = new();
        ConfiguracionInicializacionEsquemaMensajeria configuracion =
            CrearConfiguracion(baseDatos);

        await inicializador.InicializarAsync(configuracion, CancellationToken.None);

        await using (MensajeriaContextoDB modificacion = baseDatos.CrearContexto())
        {
            DAOEstadoIntentoEnvioMensajeInfobip estado = await modificacion
                .EstadosIntentoEnvioMensajeInfobip
                .SingleAsync(actual => actual.ID == "aceptado");
            estado.Descripcion = "Descripción personalizada de prueba";
            await modificacion.SaveChangesAsync();
        }

        await inicializador.InicializarAsync(configuracion, CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        int cantidadTablas = await ContarTablasInfobipAsync(
            verificacion,
            baseDatos.Esquema);
        DAOEstadoIntentoEnvioMensajeInfobip estadoPersistido = await verificacion
            .EstadosIntentoEnvioMensajeInfobip
            .AsNoTracking()
            .SingleAsync(actual => actual.ID == "aceptado");

        Assert.Equal(36, cantidadTablas);
        Assert.Equal(
            "Descripción personalizada de prueba",
            estadoPersistido.Descripcion);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task Inicializar_EstructuraParcial_DebeFallarConTablaFaltante(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        InicializadorModuloEsquemaInfobip inicializador = new();
        ConfiguracionInicializacionEsquemaMensajeria configuracion =
            CrearConfiguracion(baseDatos);
        await inicializador.InicializarAsync(configuracion, CancellationToken.None);

        await using (MensajeriaContextoDB modificacion = baseDatos.CrearContexto())
        {
            await EliminarTablaTextoAsync(
                modificacion,
                baseDatos.Esquema,
                motor);
        }

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inicializador.InicializarAsync(configuracion, CancellationToken.None));

        Assert.Contains(
            "per_text_messages_infobip",
            excepcion.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ConfiguracionInicializacionEsquemaMensajeria
        CrearConfiguracion(BaseDatosPrueba baseDatos)
    {
        return new ConfiguracionInicializacionEsquemaMensajeria
        {
            Proveedor = baseDatos.Motor == MotorBaseDatosPrueba.PostgreSql
                ? ProveedorBaseDatosMensajeria.PostgreSql
                : ProveedorBaseDatosMensajeria.SqlServer,
            CadenaConexion = baseDatos.ConnectionString,
            Esquema = baseDatos.Esquema
        };
    }

    private static async Task<int> ContarTablasInfobipAsync(
        MensajeriaContextoDB contexto,
        string esquema)
    {
        await contexto.Database.OpenConnectionAsync();
        DbConnection conexion = contexto.Database.GetDbConnection();
        await using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = @esquema
              AND table_name LIKE 'per_%_infobip'
            """;
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = "@esquema";
        parametro.Value = esquema;
        comando.Parameters.Add(parametro);
        object? resultado = await comando.ExecuteScalarAsync();
        return Convert.ToInt32(resultado);
    }

    private static async Task EliminarTablaTextoAsync(
        MensajeriaContextoDB contexto,
        string esquema,
        MotorBaseDatosPrueba motor)
    {
        await contexto.Database.OpenConnectionAsync();
        DbConnection conexion = contexto.Database.GetDbConnection();
        await using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = motor == MotorBaseDatosPrueba.PostgreSql
            ? $"DROP TABLE \"{esquema}\".\"per_text_messages_infobip\""
            : $"DROP TABLE [{esquema}].[per_text_messages_infobip]";
        await comando.ExecuteNonQueryAsync();
    }
}
