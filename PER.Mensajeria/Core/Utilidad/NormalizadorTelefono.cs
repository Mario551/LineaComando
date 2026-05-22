namespace PER.Mensajeria.Core.Utilidad;

public static class NormalizadorTelefono
{
    public static string Normalizar(string telefono)
    {
        return telefono
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
    }
}
