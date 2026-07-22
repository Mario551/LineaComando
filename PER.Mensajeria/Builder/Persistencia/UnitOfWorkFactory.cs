using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Datos.UnitOfWork;

namespace PER.Mensajeria.Builder.Persistencia;

public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IServiceScopeFactory serviceScopeFactory;

    public UnitOfWorkFactory(IServiceScopeFactory serviceScopeFactory)
    {
        this.serviceScopeFactory = serviceScopeFactory;
    }

    public IUnitOfWorkScope Crear()
    {
        AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();

        try
        {
            IUnitOfWork unitOfWork = alcance.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return new UnitOfWorkScope(alcance, unitOfWork);
        }
        catch
        {
            alcance.Dispose();
            throw;
        }
    }
}
