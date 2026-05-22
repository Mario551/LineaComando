using PER.Comandos.LineaComandos.Cola.Almacen;

namespace PER.Comandos.LineaComandos.Cola.Colas
{
    public sealed class ComandoEncolado
    {
        public long ComandoId { get; init; }

        public Task<ResultadoComando> Resultado { get; init; } = Task.FromResult(
            ResultadoComando.Fallo("El comando no tiene resultado asociado."));
    }
}
