using Microsoft.Extensions.DependencyInjection;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;

namespace BuilderTest;

public class LineaComandoExtensionsTest
{
    [Fact]
    public async Task InicializarLineaComandoAsync_DebeConfigurarTodasLasFactoriasAntesDeConstruir()
    {
        ServiceCollection servicios = new();
        List<string> ejecuciones = [];
        RegistroComandosPrueba registro = new(ejecuciones);

        servicios.AddLineaComando("pedido", async (_, inicializador, _) =>
        {
            ejecuciones.Add("pedido");
            var builder = inicializador.NewBuilderComando();
            await builder
                .Argumentos("consultar", null)
                .Accion(_ => new ComandoPrueba())
                .RegistrarAsync();
            await builder
                .New()
                .Argumentos("cancelar", null)
                .Accion(_ => new ComandoPrueba())
                .RegistrarAsync();
        });
        servicios.AddLineaComando("cliente", async (_, inicializador, _) =>
        {
            ejecuciones.Add("cliente");
            await inicializador
                .NewBuilderComando()
                .Argumentos("consultar", null)
                .Accion(_ => new ComandoPrueba())
                .RegistrarAsync();
        });

        LineaComandoBuilder builder = servicios.AddLineaComando();
        builder.AgregarInicializadorExterno((_, _, _) =>
        {
            ejecuciones.Add("externo");
            return Task.CompletedTask;
        });

        servicios.AddSingleton(builder);
        servicios.AddSingleton<IRegistroComandos<string, ResultadoComando>>(registro);
        servicios.AddSingleton<FactoriaAbstractaComandos<string, ResultadoComando>>();
        servicios.AddSingleton<IFactoriaAbstractaComandos<string, ResultadoComando>>(
            provider => provider.GetRequiredService<FactoriaAbstractaComandos<string, ResultadoComando>>());

        using ServiceProvider provider = servicios.BuildServiceProvider();

        await provider.InicializarLineaComandoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["externo", "pedido", "cliente", "factoria"], ejecuciones);
        Assert.Equal(["pedido consultar", "pedido cancelar", "cliente consultar"], registro.Rutas);
        Assert.Equal(1, registro.Construcciones);
    }

    [Fact]
    public void Build_DebeRegistrarLaFactoriaAbstractaComoInstanciaCompartida()
    {
        ServiceCollection servicios = new();
        servicios.AddLineaComando("pedido", (_, _, _) => Task.CompletedTask);
        LineaComandoBuilder builder = servicios
            .AddLineaComando()
            .UsePostgresql("Host=localhost;Database=no_utilizada");
        builder.Build();

        using ServiceProvider provider = servicios.BuildServiceProvider();
        FactoriaAbstractaComandos<string, ResultadoComando> implementacion =
            provider.GetRequiredService<FactoriaAbstractaComandos<string, ResultadoComando>>();
        IFactoriaAbstractaComandos<string, ResultadoComando> abstraccion =
            provider.GetRequiredService<IFactoriaAbstractaComandos<string, ResultadoComando>>();

        Assert.Same(implementacion, abstraccion);
        Assert.Equal("pedido", abstraccion.Get("pedido").Nombre);
    }

    [Fact]
    public void ConfiguracionLargaDuracion_ConValoresInvalidos_DebeLanzarExcepcion()
    {
        LineaComandoBuilder builder = new ServiceCollection().AddLineaComando();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.SetUmbralComandoLargaDuracion(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.SetMaxTareasLargaDuracion(0));
    }

    private sealed class RegistroComandosPrueba : IRegistroComandos<string, ResultadoComando>
    {
        private readonly IList<string> _ejecuciones;

        public RegistroComandosPrueba(IList<string> ejecuciones)
        {
            _ejecuciones = ejecuciones;
        }

        public IDictionary<string, MetadatosComando> ComandosRegistrados { get; } =
            new Dictionary<string, MetadatosComando>();

        public List<string> Rutas { get; } = [];

        public int Construcciones { get; private set; }

        public Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(
            CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<MetadatosComando>>(ComandosRegistrados.Values);
        }

        public Task ConstruirFactoriaAsync(
            IFactoriaAbstractaComandos<string, ResultadoComando> factoria,
            CancellationToken token = default)
        {
            Assert.Equal("pedido", factoria.Get("pedido").Nombre);
            Assert.Equal("cliente", factoria.Get("cliente").Nombre);
            Assert.Equal(3, ComandosRegistrados.Count);
            Construcciones++;
            _ejecuciones.Add("factoria");
            return Task.CompletedTask;
        }

        public Task RegistrarComandoAsync(
            MetadatosComando metadatos,
            IComandoCreador<string, ResultadoComando> comandoCreador,
            CancellationToken token = default)
        {
            ComandosRegistrados[metadatos.RutaComando] = metadatos;
            Rutas.Add(metadatos.RutaComando);
            return Task.CompletedTask;
        }

        public Task EliminarRegistroComandoAsync(
            string rutaComando,
            CancellationToken token = default)
        {
            ComandosRegistrados.Remove(rutaComando);
            return Task.CompletedTask;
        }
    }

    private sealed class ComandoPrueba : ComandoBase<string, ResultadoComando>
    {
        public override void Preparar(ICollection<PER.Comandos.LineaComandos.Atributo.Parametro> parametros)
        {
        }

        public override Task<ResultadoComando> EjecutarAsync(
            string entrada,
            CancellationToken token = default)
        {
            return Task.FromResult(ResultadoComando.Exito());
        }
    }
}
