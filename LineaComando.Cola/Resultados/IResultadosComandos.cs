using PER.Comandos.LineaComandos.Cola.Almacen;

namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public interface IResultadosComandos
    {
        Task<ResultadoComando?> ObtenerResultadoAsync(long comandoId, CancellationToken token = default);
    }
}
