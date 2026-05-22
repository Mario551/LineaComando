using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Servicio;

namespace EventDrivenTest
{
    public class CoordinadorTareasProgramadasTest
    {
        [Fact]
        public void DebeEjecutarse_ConExpresionVacia_DebeRegistrarWarningYRetornarFalse()
        {
            Mock<IServiceScopeFactory> scopeFactory = new Mock<IServiceScopeFactory>();
            Mock<ILogger<CoordinadorTareasProgramadas>> logger = new Mock<ILogger<CoordinadorTareasProgramadas>>();
            CoordinadorTareasProgramadasPrueba coordinador = new CoordinadorTareasProgramadasPrueba(
                scopeFactory.Object,
                logger.Object);

            ConfiguracionManejador config = new ConfiguracionManejador
            {
                IDManejador = 10,
                RutaComando = "comando prueba",
                Codigo = "codigo_prueba",
                Expresion = string.Empty,
                Activo = true
            };

            bool debeEjecutarse = coordinador.DebeEjecutarsePublico(config);

            Assert.False(debeEjecutarse);
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("expresión está vacía")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private sealed class CoordinadorTareasProgramadasPrueba : CoordinadorTareasProgramadas
        {
            public CoordinadorTareasProgramadasPrueba(
                IServiceScopeFactory serviceScopeFactory,
                ILogger<CoordinadorTareasProgramadas> logger)
                : base(serviceScopeFactory, logger)
            {
            }

            public bool DebeEjecutarsePublico(ConfiguracionManejador config)
            {
                return DebeEjecutarse(config);
            }
        }
    }
}
