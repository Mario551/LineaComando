using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;

namespace PER.Comandos.LineaComandos.Builder
{
    public static class LineaComandoExtensions
    {
        public static LineaComandoBuilder AddLineaComando(
            this IServiceCollection services,
            string connectionString)
        {
            return new LineaComandoBuilder(services, connectionString);
        }

        public static async Task InicializarLineaComandoAsync(
            this IHost host,
            CancellationToken token = default)
        {
            var builder = host.Services.GetRequiredService<LineaComandoBuilder>();

            if (builder.UsarCola)
            {
                var inicializadorCola = new InicializadorEsquema(builder.ConnectionString);
                await inicializadorCola.InicializarAsync(token);
            }

            if (builder.UsarEventDriven)
            {
                var inicializadorEventDriven = new InicializadorEsquemaEventDriven(builder.ConnectionString);
                await inicializadorEventDriven.InicializarAsync(token);
            }
        }
    }
}
