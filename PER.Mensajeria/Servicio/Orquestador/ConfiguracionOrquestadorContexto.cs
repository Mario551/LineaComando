namespace PER.Mensajeria.Servicio.Orquestador;

public sealed class ConfiguracionOrquestadorContexto
{
    public int MaximoConversacionesConcurrentes { get; set; } = 16;
}
