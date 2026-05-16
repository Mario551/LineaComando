namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public interface IProcesadorResultadoComando
    {
        string Tipo { get; }

        int Version { get; }

        string Formato { get; }

        Task<string?> SerializarAsync(object? salida, CancellationToken token = default);

        Task<object?> DeserializarAsync(string? contenido, CancellationToken token = default);
    }
}
