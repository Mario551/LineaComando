using PER.Mensajeria.Datos.Repositorio.Conversacion;
using PER.Mensajeria.Datos.Repositorio.EnvioMensaje;
using PER.Mensajeria.Datos.Repositorio.Mensaje;
using PER.Mensajeria.Datos.Repositorio.ProcesamientoInternoMensaje;

namespace PER.Mensajeria.Datos.UnitOfWork;

public interface IUnitOfWork
{
    IMensajeRepositorio MensajeRepositorio { get; }
    IConversacionRepositorio ConversacionRepositorio { get; }
    IProcesamientoInternoMensajeRepositorio ProcesamientoInternoMensajeRepositorio { get; }
    IEnvioMensajeRepositorio EnvioMensajeRepositorio { get; }

    Task<int> SaveChangesAsync();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}
