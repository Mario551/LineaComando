namespace PER.Mensajeria.Datos.UnitOfWork;

public interface IUnitOfWorkScope : IAsyncDisposable
{
    IUnitOfWork UnitOfWork { get; }
}
