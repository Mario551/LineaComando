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
            this IHost host,
            CancellationToken token = default)
        {
            var builder = host.Services.GetRequiredService<LineaComandoBuilder>();

            if (builder.UsarCola)
            {
                var inicializadorCola = new InicializadorEsquema(builder.ConnectionString);
                await inicializadorCola.InicializarAsync(token);

                if (builder.ConfigurarComandos != null)
                {
                    var registroComandos = host.Services.GetRequiredService<IRegistroComandos<string, ResultadoComando>>();
                    await builder.ConfigurarComandos(host.Services, registroComandos, token);
                }
            }

            if (builder.UsarEventDriven)
            {
                var inicializadorEventDriven = new InicializadorEsquemaEventDriven(builder.ConnectionString);
                await inicializadorEventDriven.InicializarAsync(token);

                if (builder.ConfigurarEventos != null)
                {
                    var registroTiposEvento = host.Services.GetRequiredService<IRegistroTiposEvento>();
                    await builder.ConfigurarEventos!(host.Services, registroTiposEvento, token);
                }
            }
        }
    }
}
