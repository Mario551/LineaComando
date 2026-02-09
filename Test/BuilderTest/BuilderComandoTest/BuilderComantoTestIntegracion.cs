using Dapper;
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

    private readonly IRegistroComandos<string, ResultadoComando> _registroComandos;

    public BuilderComandoTestIntegracion(DatabaseFixture fixture) : base(fixture)
    {
        _registroComandos = new RegistroComandos<string, ResultadoComando>(ConnectionString);
    }

    [Fact]
    public async Task RegistrarAsync_DebeInsertarComandoEnBaseDeDatos()
    {
        string rutaComando = PrefijoTest + "test_registro";
        string descripcion = "Comando de prueba para BuilderComando";

        var builderComando = new BuilderComando(_registroComandos);
        builderComando
            .Argumentos(rutaComando, descripcion)
            .Accion<string, ResultadoComando>((parametros) => new ComandoPrueba());

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
}