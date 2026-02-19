using Dapper;
using Microsoft.Extensions.DependencyInjection;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Stream;
using ComandosColaTest.Helpers;

namespace BuilderTest.BuilderComandoTest;

[Collection("Database")]
public class BuilderComandoTestIntegracion : BaseIntegracionTestBuilder
{
    protected override string PrefijoTest => "builder_cmd_";

    private readonly ServiceProvider _serviceProvider;

    public BuilderComandoTestIntegracion(DatabaseFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(
            new RegistroComandos<string, ResultadoComando>(ConnectionString));
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarComandoEnBaseDeDatos_AccionFunc()
    {
        string rutaComando = PrefijoTest + "test_registro";
        string descripcion = "Comando de prueba para BuilderComando";

        var builderComando = new BuilderComando(_serviceProvider);
        builderComando
            .Argumentos(rutaComando, descripcion)
            .Accion((parametros) => new ComandoPrueba());

        await builderComando.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM per_comandos_registrados WHERE ruta_comando = @Ruta",
            new { Ruta = rutaComando });

        Assert.NotNull(comandoDb);
        Assert.Equal(rutaComando, (string)comandoDb.ruta_comando);
        Assert.Equal(descripcion, (string)comandoDb.descripcion);
        Assert.True((bool)comandoDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarComandoEnBaseDeDatos_AccionComandoBase()
    {
        string rutaComando = PrefijoTest + "test_registro";
        string descripcion = "Comando de prueba para BuilderComando";

        var builderComando = new BuilderComando(_serviceProvider);
        builderComando
            .Argumentos(rutaComando, descripcion)
            .Accion(new ComandoPrueba());

        await builderComando.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM per_comandos_registrados WHERE ruta_comando = @Ruta",
            new { Ruta = rutaComando });

        Assert.NotNull(comandoDb);
        Assert.Equal(rutaComando, (string)comandoDb.ruta_comando);
        Assert.Equal(descripcion, (string)comandoDb.descripcion);
        Assert.True((bool)comandoDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_SinArgumentosYAccion_DebeLanzarExcepcion()
    {
        var builderComando = new BuilderComando(_serviceProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await builderComando.RegistrarAsync();
        });
    }
}