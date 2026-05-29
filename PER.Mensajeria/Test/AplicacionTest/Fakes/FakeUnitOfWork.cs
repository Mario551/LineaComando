using PER.Mensajeria.Datos.Repositorio.Conversacion;
using PER.Mensajeria.Datos.Repositorio.EnvioMensaje;
using PER.Mensajeria.Datos.Repositorio.Mensaje;
using PER.Mensajeria.Datos.Repositorio.ProcesamientoInternoMensaje;
using PER.Mensajeria.Datos.UnitOfWork;

namespace AplicacionTest.Fakes;

public class FakeUnitOfWork : IUnitOfWork
{
    public IMensajeRepositorio MensajeRepositorio => throw new NotImplementedException();
    public IConversacionRepositorio ConversacionRepositorio => throw new NotImplementedException();
    public IProcesamientoInternoMensajeRepositorio ProcesamientoInternoMensajeRepositorio => throw new NotImplementedException();
    public IEnvioMensajeRepositorio EnvioMensajeRepositorio => throw new NotImplementedException();

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(0);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
