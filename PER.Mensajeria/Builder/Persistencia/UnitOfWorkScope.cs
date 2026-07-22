using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Datos.UnitOfWork;

namespace PER.Mensajeria.Builder.Persistencia;

public class UnitOfWorkScope : IUnitOfWorkScope
{
    private readonly AsyncServiceScope alcance;

    public UnitOfWorkScope(AsyncServiceScope alcance, IUnitOfWork unitOfWork)
    {
        this.alcance = alcance;
        UnitOfWork = unitOfWork;
    }

    public IUnitOfWork UnitOfWork { get; }

    public ValueTask DisposeAsync()
    {
        return alcance.DisposeAsync();
    }
}
