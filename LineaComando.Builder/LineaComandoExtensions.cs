using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            string connectionString)
        {
            return new LineaComandoBuilder(services, connectionString);
        }

        public static async Task InicializarLineaComandoAsync(
            this IServiceProvider services,
            CancellationToken token = default)
        {
            var builder = services.GetRequiredService<LineaComandoBuilder>();

            if (builder.UsarCola)
            {
                var inicializadorCola = new InicializadorEsquema(builder.ConnectionString);
                await inicializadorCola.InicializarAsync(token);

                if (builder.ConfigurarComandos != null)
                {
                    var registroComandos = services.GetRequiredService<IRegistroComandos<string, ResultadoComando>>();
                    await builder.ConfigurarComandos(services, registroComandos, token);
                }
            }

            if (builder.UsarEventDriven)
            {
                var inicializadorEventDriven = new InicializadorEsquemaEventDriven(builder.ConnectionString);
                await inicializadorEventDriven.InicializarAsync(token);

                if (builder.ConfigurarEventos != null)
                {
                    var registroTiposEvento = services.GetRequiredService<IRegistroTiposEvento>();
                    await builder.ConfigurarEventos!(services, registroTiposEvento, token);
                }
            }
        }
    }
}
