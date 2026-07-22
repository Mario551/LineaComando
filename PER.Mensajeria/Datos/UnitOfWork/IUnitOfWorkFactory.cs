namespace PER.Mensajeria.Datos.UnitOfWork;

public interface IUnitOfWorkFactory
{
    IUnitOfWorkScope Crear();
}
