using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderInicializador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Registro;
using PER.Comandos.LineaComandos.FactoriaComandos;
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

            if (builder.TipoBaseDatos == LineaComandoBuilder.POSTGRESQL)
            {
                var inicializadorCola = new InicializadorEsquemaPostgres(builder.ConnectionString, builder.EsquemaBaseDatos);
                await inicializadorCola.InicializarAsync(token);
            }
            else if (builder.TipoBaseDatos == LineaComandoBuilder.SQLSERVER)
            {
                var inicializadorCola = new InicializadorEsquemaSqlServer(builder.ConnectionString, builder.EsquemaBaseDatos);
                await inicializadorCola.InicializarAsync(token);
            }

            if (builder.TipoBaseDatos == LineaComandoBuilder.POSTGRESQL)
            {
                var inicializadorEventDriven = new InicializadorEsquemaEventDrivenPostgres(builder.ConnectionString, builder.EsquemaBaseDatos);
                await inicializadorEventDriven.InicializarAsync(token);
            }
            else if (builder.TipoBaseDatos == LineaComandoBuilder.SQLSERVER)
            {
                var inicializadorEventDriven = new InicializadorEsquemaEventDrivenSqlServer(builder.ConnectionString, builder.EsquemaBaseDatos);
                await inicializadorEventDriven.InicializarAsync(token);
            }

            await builder.ConfiguracionLineaComandos(services, new BuilderInicializador.BuilderInicializador(services), token);

            var registroComandos = services.GetRequiredService<IRegistroComandos<string, ResultadoComando>>();
            var factoriaComandos = services.GetRequiredService<FactoriaComandos<string, ResultadoComando>>();
            await registroComandos.ConstruirFactoriaAsync(factoriaComandos, token);
        }
    }
}
