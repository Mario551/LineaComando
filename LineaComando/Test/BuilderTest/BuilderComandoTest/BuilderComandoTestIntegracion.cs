using Dapper;
using Microsoft.Extensions.DependencyInjection;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Stream;
using ComandosColaTest.Helpers;

namespace BuilderTest.BuilderComandoTest;

[Collection("Database")]
public class BuilderComandoTestIntegracion : BaseIntegracionTestBuilder
{
    protected override string PrefijoTest => "builder_cmd_";
    private string NombreFactoria => PrefijoTest + "factoria";

    private readonly ServiceProvider _serviceProvider;

    public BuilderComandoTestIntegracion(DatabaseFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(
            new RegistroComandosPostgres<string, ResultadoComando>(ConnectionString, Esquema));
        services.AddSingleton<
            IRegistroProcesadoresSerializacionResultadosComando,
            RegistroProcesadoresSerializacionResultadosComando>();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarComandoEnBaseDeDatos_AccionFunc()
    {
        string rutaRelativa = "test_registro_func";
        string rutaComando = $"{NombreFactoria} {rutaRelativa}";
        string descripcion = "Comando de prueba para BuilderComando";

        var builderComando = new BuilderComando(_serviceProvider, NombreFactoria);
        builderComando
            .Argumentos(rutaRelativa, descripcion)
            .Accion((parametros) => new ComandoPrueba());

        await builderComando.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            $"SELECT * FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
            new { Ruta = rutaComando });

        Assert.NotNull(comandoDb);
        Assert.Equal(rutaComando, (string)comandoDb.ruta_comando);
        Assert.Equal(descripcion, (string)comandoDb.descripcion);
        Assert.True((bool)comandoDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarComandoEnBaseDeDatos_AccionComandoBase()
    {
        string rutaRelativa = "test_registro_base";
        string rutaComando = $"{NombreFactoria} {rutaRelativa}";
        string descripcion = "Comando de prueba para BuilderComando";

        var builderComando = new BuilderComando(_serviceProvider, NombreFactoria);
        builderComando
            .Argumentos(rutaRelativa, descripcion)
            .Accion(new ComandoPrueba());

        await builderComando.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            $"SELECT * FROM {Nombres.ComandosRegistrados} WHERE ruta_comando = @Ruta",
            new { Ruta = rutaComando });

        Assert.NotNull(comandoDb);
        Assert.Equal(rutaComando, (string)comandoDb.ruta_comando);
        Assert.Equal(descripcion, (string)comandoDb.descripcion);
        Assert.True((bool)comandoDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_ConResultado_DebeRegistrarProcesador()
    {
        string rutaRelativa = "test_resultado";
        string rutaComando = $"{NombreFactoria} {rutaRelativa}";
        ProcesadorResultadoTexto procesador = new ProcesadorResultadoTexto();

        var builderComando = new BuilderComando(_serviceProvider, NombreFactoria);
        builderComando
            .Argumentos(rutaRelativa, "Comando con resultado")
            .Accion(new ComandoPrueba())
            .Resultado(procesador);

        await builderComando.RegistrarAsync();

        IRegistroProcesadoresSerializacionResultadosComando registroProcesadoresSerializacionResultados =
            _serviceProvider.GetRequiredService<IRegistroProcesadoresSerializacionResultadosComando>();

        Assert.Same(
            procesador,
            registroProcesadoresSerializacionResultados.ObtenerPorRutaComando(rutaComando));
        Assert.Same(
            procesador,
            registroProcesadoresSerializacionResultados.ObtenerPorTipoVersion(
                procesador.Tipo,
                procesador.Version));
    }

    [Fact]
    public async Task RegistrarAsync_SinArgumentosYAccion_DebeLanzarExcepcion()
    {
        var builderComando = new BuilderComando(_serviceProvider, NombreFactoria);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await builderComando.RegistrarAsync();
        });
    }

    [Fact]
    public void Argumentos_ConParametroEnRuta_DebeLanzarExcepcion()
    {
        var builderComando = new BuilderComando(_serviceProvider, NombreFactoria);

        Assert.Throws<ArgumentException>(() =>
            builderComando.Argumentos("consultar --id=1", null));
    }
}
