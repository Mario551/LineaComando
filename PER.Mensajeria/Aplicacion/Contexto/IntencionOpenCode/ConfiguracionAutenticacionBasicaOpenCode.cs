namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class ConfiguracionAutenticacionBasicaOpenCode
{
    public ConfiguracionAutenticacionBasicaOpenCode(
        string usuario,
        string contrasena)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasena);

        Usuario = usuario;
        Contrasena = contrasena;
    }

    public string Usuario { get; }
    public string Contrasena { get; }
}
