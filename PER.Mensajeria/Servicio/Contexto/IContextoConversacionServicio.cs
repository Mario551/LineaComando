namespace PER.Mensajeria.Servicio.Contexto;

public interface IContextoConversacionServicio
{
    Task ResolverAsync(CancellationToken cancellationToken);
}
