using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using BuilderTest.BuilderComandoTest;
using PER.Comandos.LineaComandos.BuilderTipoEvento;
using PER.Comandos.LineaComandos.EventDriven.Registro;

namespace BuilderTest.BuilderTipoEventoTest;

[Collection("Database")]
public class BuilderTipoEventoTestIntegracion : BaseIntegracionTestBuilder
{
    protected override string PrefijoTest => "builder_tipo_evento_";

    private readonly ServiceProvider _serviceProvider;

    public BuilderTipoEventoTestIntegracion(DatabaseFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegistroTiposEvento>(
            new RegistroTiposEventoPostgres(ConnectionString));
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarTipoEventoEnBaseDeDatos()
    {
        string codigoTipoEvento = "builder_tipo_evento_test";
        string nombreTipoEvento = "Tipo de Evento de Prueba";
        string descripcionTipoEvento = "Tipo de evento de prueba para BuilderTipoEvento";

        var builderTipoEvento = new BuilderTipoEvento(_serviceProvider);
        await builderTipoEvento
            .Argumentos(
                codigo: codigoTipoEvento,
                nombre: nombreTipoEvento,
                descripcion: descripcionTipoEvento)
            .RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var tipoEventoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM per_tipos_evento WHERE codigo = @Codigo",
            new { Codigo = codigoTipoEvento });

        Assert.NotNull(tipoEventoDb);
        Assert.Equal(codigoTipoEvento, (string)tipoEventoDb.codigo);
        Assert.Equal(nombreTipoEvento, (string)tipoEventoDb.nombre);
        Assert.Equal(descripcionTipoEvento, (string)tipoEventoDb.descripcion);
        Assert.True((bool)tipoEventoDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_SinArgumentos_DebeLanzarExcepcion()
    {
        var builderTipoEvento = new BuilderTipoEvento(_serviceProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await builderTipoEvento.RegistrarAsync();
        });
    }

    protected override async Task LimpiarDatosDelTestAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM per_tipos_evento WHERE codigo LIKE @Prefijo;",
            new { Prefijo = PrefijoTest + "%" });
    }
}
