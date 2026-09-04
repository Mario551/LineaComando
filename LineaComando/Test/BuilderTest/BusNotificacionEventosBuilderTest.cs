using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.Cola.Notificaciones;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.EventDriven.Bus;

namespace BuilderTest
{
    public class BusNotificacionEventosBuilderTest
    {
        [Fact]
        public void Build_DebeRegistrarUnaInstanciaCompartidaDelBus()
        {
            ServiceCollection servicios = new ServiceCollection();
            servicios.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            servicios.AddSingleton<ILogger<ProcesadorColaComandos>>(
                NullLogger<ProcesadorColaComandos>.Instance);
            LineaComandoBuilder builder = servicios.AddLineaComando();
            builder.UsePostgresql("Host=localhost;Database=no_utilizada");
            builder.Build();

            using ServiceProvider proveedor = servicios.BuildServiceProvider();
            BusNotificacionEventosEnMemoria implementacion =
                proveedor.GetRequiredService<BusNotificacionEventosEnMemoria>();
            IBusNotificacionEventos bus =
                proveedor.GetRequiredService<IBusNotificacionEventos>();
            IPublicadorNotificacionEventos publicador =
                proveedor.GetRequiredService<IPublicadorNotificacionEventos>();
            BusNotificacionEjecucionComandosEnMemoria implementacionComandos =
                proveedor.GetRequiredService<BusNotificacionEjecucionComandosEnMemoria>();
            IBusNotificacionEjecucionComandos busComandos =
                proveedor.GetRequiredService<IBusNotificacionEjecucionComandos>();
            IPublicadorNotificacionEjecucionComandos publicadorComandos =
                proveedor.GetRequiredService<IPublicadorNotificacionEjecucionComandos>();

            Assert.Same(implementacion, bus);
            Assert.Same(implementacion, publicador);
            Assert.Same(bus, proveedor.GetRequiredService<IBusNotificacionEventos>());
            Assert.Same(implementacionComandos, busComandos);
            Assert.Same(implementacionComandos, publicadorComandos);
            Assert.Same(
                busComandos,
                proveedor.GetRequiredService<IBusNotificacionEjecucionComandos>());
            Assert.NotNull(proveedor.GetRequiredService<ProcesadorColaComandos>());
        }
    }
}
