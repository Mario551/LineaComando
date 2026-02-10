using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using BuilderTest.BuilderComandoTest;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderManejador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.Registro;
using ComandosColaTest.Helpers;

namespace BuilderTest.BuilderManejadorTest;

[Collection("Database")]
public class BuilderManejadorTestIntegracion : BaseIntegracionTestBuilder
{
    protected override string PrefijoTest => "builder_manejador_";

    private readonly ServiceProvider _serviceProvider;

    public BuilderManejadorTestIntegracion(DatabaseFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(
            new RegistroComandos<string, ResultadoComando>(ConnectionString));
        services.AddSingleton<IRegistroManejadores>(
            new RegistroManejadores(ConnectionString));
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarManejadorEnBaseDatos()
    {
        string rutaComando = PrefijoTest + "cmd_test";
        string codigoManejador = "builder_manejador_test";
        string nombreManejador = "Manejador de Prueba";
        string descripcionManejador = "Manejador de prueba para BuilderManejador";
        string argumentosComando = "--param1 valor1 --param2 valor2";

        MetadatosComando metadatosComando = await CrearComandoAsync(rutaComando);

        var builderManejador = new BuilderManejador(metadatosComando, _serviceProvider);
        builderManejador.Argumentos(
            codigo: codigoManejador,
            nombre: nombreManejador,
            argumentosComando: argumentosComando,
            descripcion: descripcionManejador);

        await builderManejador.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var manejadorDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT * FROM per_manejadores_evento 
            WHERE codigo = @Codigo AND id_comando_registrado = @IdComando",
            new { Codigo = codigoManejador, IdComando = metadatosComando.Id });

        Assert.NotNull(manejadorDb);
        Assert.Equal(codigoManejador, (string)manejadorDb.codigo);
        Assert.Equal(nombreManejador, (string)manejadorDb.nombre);
        Assert.Equal(descripcionManejador, (string)manejadorDb.descripcion);
        Assert.Equal(metadatosComando.Id, (int)manejadorDb.id_comando_registrado);
        Assert.Equal(rutaComando, (string)manejadorDb.ruta_comando);
        Assert.Equal(argumentosComando, (string)manejadorDb.argumentos_comando);
        Assert.True((bool)manejadorDb.activo);
    }

    private async Task<MetadatosComando> CrearComandoAsync(string rutaComando)
    {
        var builderComando = new BuilderComando(_serviceProvider);
        builderComando
            .Argumentos(rutaComando, "Comando de prueba para manejador")
            .Accion<string, ResultadoComando>((parametros) => new ComandoPrueba());

        var builderManejador = await builderComando.RegistrarAsync();
        
        using var connection = CrearConexion();
        await connection.OpenAsync();
        
        var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT id, ruta_comando, descripcion, activo, creado_en FROM per_comandos_registrados WHERE ruta_comando = @Ruta",
            new { Ruta = rutaComando });

        Assert.NotNull(comandoDb);
        
        return new MetadatosComando
        {
            Id = (int)comandoDb.id,
            RutaComando = (string)comandoDb.ruta_comando,
            Descripcion = (string)comandoDb.descripcion,
            Activo = (bool)comandoDb.activo,
            CreadoEn = (DateTime)comandoDb.creado_en
        };
    }

    protected override async Task LimpiarDatosDelTestAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            @"DELETE FROM per_manejadores_evento 
            WHERE codigo LIKE @Prefijo OR ruta_comando LIKE @PrefijoCmd",
            new { 
                Prefijo = PrefijoTest + "%",
                PrefijoCmd = PrefijoTest + "%"
            });

        await connection.ExecuteAsync(
            "DELETE FROM per_comandos_registrados WHERE ruta_comando LIKE @Prefijo;",
            new { Prefijo = PrefijoTest + "%" });
    }

    [Fact]
    public async Task RegistrarAsync_SinArgumentos_DebeLanzarExcepcion()
    {
        string rutaComando = PrefijoTest + "cmd_test_error";
        MetadatosComando metadatosComando = await CrearComandoAsync(rutaComando);

        var builderManejador = new BuilderManejador(metadatosComando, _serviceProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await builderManejador.RegistrarAsync();
        });
    }
}
