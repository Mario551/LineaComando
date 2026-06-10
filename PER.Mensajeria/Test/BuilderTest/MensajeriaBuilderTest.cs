using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Datos.Contexto;

namespace BuilderTest;

public class MensajeriaBuilderTest
{
    [Fact]
    public void AgregarWorkerOrquestador_DebeRegistrarHostedService()
    {
        ServiceCollection servicios = new();

        servicios.AgregarMensajeria(builder => builder.AgregarWorkerOrquestador());

        Assert.Contains(servicios, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AgregarMensajeria_PostgreSql_DebeUsarConexionEsquemaYRegistrarInicializador()
    {
        ServiceCollection servicios = new();
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) => Task.CompletedTask);
        lineaComandoBuilder.UsePostgresql(
            "Host=localhost;Database=lineacomando;Username=postgres;Password=123456789",
            "mensajeria_test");

        lineaComandoBuilder.AgregarMensajeria(_ => { });

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        using MensajeriaContextoDB contexto = serviceProvider.GetRequiredService<MensajeriaContextoDB>();
        NpgsqlConnectionStringBuilder builderConexion = new(contexto.Database.GetDbConnection().ConnectionString);

        Assert.Equal("mensajeria_test", builderConexion.SearchPath);
        Assert.Equal("mensajeria_test", contexto.Model.GetDefaultSchema());
        Assert.Single(lineaComandoBuilder.InicializadoresExternos);
    }

    [Fact]
    public void AgregarMensajeria_SqlServer_DebeUsarConexionEsquemaYRegistrarInicializador()
    {
        ServiceCollection servicios = new();
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) => Task.CompletedTask);
        lineaComandoBuilder.UseSqlServer(
            "Server=localhost;Database=lineacomando;User Id=sa;Password=ClaveTemporal123;TrustServerCertificate=True",
            "mensajeria_sql");

        lineaComandoBuilder.AgregarMensajeria(_ => { });

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        using MensajeriaContextoDB contexto = serviceProvider.GetRequiredService<MensajeriaContextoDB>();

        Assert.Contains("SqlServer", contexto.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mensajeria_sql", contexto.Model.GetDefaultSchema());
        Assert.Single(lineaComandoBuilder.InicializadoresExternos);
    }

    [Fact]
    public void AgregarMensajeria_Sqlite_DebeRechazarMotorNoSoportado()
    {
        ServiceCollection servicios = new();
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) => Task.CompletedTask);
        lineaComandoBuilder.UseSqlite("Data Source=:memory:");

        Assert.Throws<NotSupportedException>(() => lineaComandoBuilder.AgregarMensajeria(_ => { }));
    }

    [Fact]
    public async Task InicializarLineaComandoAsync_DebeEjecutarInicializadoresExternosAntesDeConfiguracionComandos()
    {
        ServiceCollection servicios = new();
        List<string> ejecuciones = [];
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) =>
        {
            ejecuciones.Add("configuracion");
            return Task.CompletedTask;
        });

        lineaComandoBuilder.AgregarInicializadorExterno((_, _, _) =>
        {
            ejecuciones.Add("externo");
            return Task.CompletedTask;
        });

        servicios.AddSingleton(lineaComandoBuilder);
        servicios.AddSingleton(new FactoriaComandos<string, ResultadoComando>());
        servicios.AddSingleton<IRegistroComandos<string, ResultadoComando>>(new RegistroComandosFake(ejecuciones));

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();

        await serviceProvider.InicializarLineaComandoAsync();

        Assert.Equal(["externo", "configuracion", "factoria"], ejecuciones);
    }

    private sealed class RegistroComandosFake : IRegistroComandos<string, ResultadoComando>
    {
        private readonly IList<string> ejecuciones;

        public RegistroComandosFake(IList<string> ejecuciones)
        {
            this.ejecuciones = ejecuciones;
        }

        public IDictionary<string, MetadatosComando> ComandosRegistrados { get; } = new Dictionary<string, MetadatosComando>();

        public Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<MetadatosComando>>([]);
        }

        public Task ConstruirFactoriaAsync(FactoriaComandos<string, ResultadoComando> factoria, CancellationToken token = default)
        {
            ejecuciones.Add("factoria");
            return Task.CompletedTask;
        }

        public Task RegistrarComandoAsync(MetadatosComando metadatos, IComandoCreador<string, ResultadoComando> comandoCreador, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task EliminarRegistroComandoAsync(string rutaComando, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
    }
}
