using PER.Comandos.LineaComandos.Atributo;

namespace PER.Comandos.LineaComandos
{
    public sealed class ResultadoArgumentosLineaComando
    {
        public ICollection<Parametro> Parametros { get; init; } = new List<Parametro>();

        public string? Data { get; init; }
    }
}
