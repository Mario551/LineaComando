using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using BuilderTest.BuilderComandoTest;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderDisparador;
using PER.Comandos.LineaComandos.BuilderManejador;
using PER.Comandos.LineaComandos.BuilderTipoEvento;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Registro;
using PER.Comandos.LineaComandos.Registro;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Registro;

namespace BuilderTest.BuilderDisparadorTest;

[Collection("Database")]
public class BuilderDisparadorTestIntegracion : BaseIntegracionTestBuilder
{
    protected override string PrefijoTest => "builder_disparador_";

    private readonly ServiceProvider _serviceProvider;

    public BuilderDisparadorTestIntegracion(DatabaseFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(
            new RegistroComandosPostgres<string, ResultadoComando>(ConnectionString));
        services.AddSingleton<IRegistroManejadores>(
            new RegistroManejadoresPostgres(ConnectionString));
        services.AddSingleton<IRegistroTiposEvento>(
            new RegistroTiposEventoPostgres(ConnectionString));
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegistrarAsync_DisparadorPorEvento_DebeInsertarEnBaseDatos()
    {
        string rutaComando = PrefijoTest + "cmd_test";
        string codigoManejador = PrefijoTest + "manejador_test";
        string codigoTipoEvento = PrefijoTest + "tipo_evento_test";
        string codigoDisparador = PrefijoTest + "disparador_test";
        int prioridad = 10;

        MetadatosComando metadatosComando = await CrearComandoAsync(rutaComando);
        int manejadorId = await CrearManejadorAsync(metadatosComando, codigoManejador);
        ITipoEvento tipoEvento = await CrearTipoEventoAsync(codigoTipoEvento, "Tipo Evento Test");

        var builderDisparador = new BuilderDisparador(manejadorId, _serviceProvider);
        builderDisparador
            .New()
            .Argumentos(codigoDisparador, prioridad, tipoEvento);

        await builderDisparador.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var disparadorDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT d.*, m.codigo as manejador_codigo 
            FROM per_disparadores_manejador d
            JOIN per_manejadores_evento m ON d.manejador_evento_id = m.id
            WHERE d.codigo = @Codigo",
            new { Codigo = codigoDisparador });

        Assert.NotNull(disparadorDb);
        Assert.Equal(codigoDisparador, (string)disparadorDb.codigo);
        Assert.Equal(manejadorId, (int)disparadorDb.manejador_evento_id);
        Assert.Equal("Evento", (string)disparadorDb.modo_disparo);
        Assert.Equal(tipoEvento.ID, (int)disparadorDb.tipo_evento_id);
        Assert.Equal(prioridad, (int)disparadorDb.prioridad);
        Assert.True((bool)disparadorDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_DisparadorProgramado_DebeInsertarEnBaseDatos()
    {
        string rutaComando = PrefijoTest + "cmd_programado";
        string codigoManejador = PrefijoTest + "manejador_programado";
        string codigoDisparador = PrefijoTest + "disparador_programado";
        string expresionCron = "0 0 * * *";
        int prioridad = 5;

        MetadatosComando metadatosComando = await CrearComandoAsync(rutaComando);
        int manejadorId = await CrearManejadorAsync(metadatosComando, codigoManejador);

        var builderDisparador = new BuilderDisparador(manejadorId, _serviceProvider);
        builderDisparador
            .New()
            .Argumentos(codigoDisparador, prioridad, expresionCron);

        await builderDisparador.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var disparadorDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT d.*, m.codigo as manejador_codigo 
            FROM per_disparadores_manejador d
            JOIN per_manejadores_evento m ON d.manejador_evento_id = m.id
            WHERE d.codigo = @Codigo",
            new { Codigo = codigoDisparador });

        Assert.NotNull(disparadorDb);
        Assert.Equal(codigoDisparador, (string)disparadorDb.codigo);
        Assert.Equal(manejadorId, (int)disparadorDb.manejador_evento_id);
        Assert.Equal("Programado", (string)disparadorDb.modo_disparo);
        Assert.Equal(expresionCron, (string)disparadorDb.expresion);
        Assert.Null(disparadorDb.tipo_evento_id);
        Assert.Equal(prioridad, (int)disparadorDb.prioridad);
        Assert.True((bool)disparadorDb.activo);
    }

    [Fact]
    public async Task RegistrarAsync_SinArgumentos_DebeLanzarExcepcion()
    {
        int manejadorId = 999;
        var builderDisparador = new BuilderDisparador(manejadorId, _serviceProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await builderDisparador.RegistrarAsync();
        });
    }

    private async Task<MetadatosComando> CrearComandoAsync(string rutaComando)
    {
        var builderComando = new BuilderComando(_serviceProvider);
        builderComando
            .Argumentos(rutaComando, "Comando de prueba para disparador")
            .Accion((parametros) => new ComandoPrueba());

        await builderComando.RegistrarAsync();

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

    private async Task<int> CrearManejadorAsync(MetadatosComando metadatosComando, string codigoManejador)
    {
        var builderManejador = new BuilderManejador(metadatosComando, _serviceProvider);
        builderManejador.Argumentos(
            codigo: codigoManejador,
            nombre: "Manejador de Prueba",
            argumentosComando: "--param1 valor1",
            descripcion: "Manejador de prueba para BuilderDisparador");

        await builderManejador.RegistrarAsync();

        using var connection = CrearConexion();
        await connection.OpenAsync();

        var manejadorDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT id FROM per_manejadores_evento WHERE codigo = @Codigo",
            new { Codigo = codigoManejador });

        Assert.NotNull(manejadorDb);

        return (int)manejadorDb.id;
    }

    private async Task<ITipoEvento> CrearTipoEventoAsync(string codigo, string nombre)
    {
        var builderTipoEvento = new BuilderTipoEvento(_serviceProvider);
        builderTipoEvento.Argumentos(codigo, nombre, "Tipo de evento de prueba");

        return await builderTipoEvento.RegistrarAsync();
    }

    protected override async Task LimpiarDatosDelTestAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            @"DELETE FROM per_disparadores_manejador 
            WHERE codigo LIKE @Prefijo",
            new { Prefijo = PrefijoTest + "%" });

        await connection.ExecuteAsync(
            @"DELETE FROM per_tipos_evento 
            WHERE codigo LIKE @Prefijo",
            new { Prefijo = PrefijoTest + "%" });

        await connection.ExecuteAsync(
            @"DELETE FROM per_manejadores_evento 
            WHERE codigo LIKE @Prefijo OR ruta_comando LIKE @PrefijoCmd",
            new
            {
                Prefijo = PrefijoTest + "%",
                PrefijoCmd = PrefijoTest + "%"
            });

        await connection.ExecuteAsync(
            "DELETE FROM per_comandos_registrados WHERE ruta_comando LIKE @Prefijo;",
            new { Prefijo = PrefijoTest + "%" });
    }
}