namespace PER.Mensajeria.Aplicacion.Contexto;

public static class ParametrosReservadosComandoContexto
{
    public const string Data = "data";

    public const string IdentificadorPropietarioContexto =
        "identificador-propietario-contexto";

    public static bool EsData(string nombre)
    {
        return string.Equals(
            QuitarPrefijoLineaComando(nombre),
            Data,
            StringComparison.Ordinal);
    }

    private static string QuitarPrefijoLineaComando(string nombre)
    {
        return nombre.StartsWith("--", StringComparison.Ordinal)
            ? nombre[2..]
            : nombre;
    }
}
