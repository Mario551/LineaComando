using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderInicializador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace PER.Comandos.LineaComandos.Builder
{
    public static class LineaComandoExtensions
    {
        public static LineaComandoBuilder AddLineaComando(
            this IServiceCollection services,
            Func<IServiceProvider, IBuilderInicializador, CancellationToken, Task> configuracionLineaComandos)
        {
            return new LineaComandoBuilder(services, configuracionLineaComandos);
        }

        public static async Task InicializarLineaComandoAsync(
            this IServiceProvider services,
            CancellationToken token = default)
        {
            var builder = services.GetRequiredService<LineaComandoBuilder>();

            var inicializadorCola = new InicializadorEsquemaPostgres(builder.ConnectionString);
            await inicializadorCola.InicializarAsync(token);

            var inicializadorEventDriven = new InicializadorEsquemaEventDriven(builder.ConnectionString);
            await inicializadorEventDriven.InicializarAsync(token);

            await builder.ConfiguracionLineaComandos(services, new BuilderInicializador.BuilderInicializador(services), token);
        }
    }
}
