using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio.Conversacion;
using PER.Mensajeria.Datos.Repositorio.EnvioMensaje;
using PER.Mensajeria.Datos.Repositorio.Mensaje;
using PER.Mensajeria.Datos.Repositorio.ProcesamientoInternoMensaje;
using Microsoft.EntityFrameworkCore.Storage;

namespace PER.Mensajeria.Datos.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly MensajeriaContextoDB contexto;
    private IDbContextTransaction? transaccion;
    private IMensajeRepositorio? mensajeRepositorio;
    private IConversacionRepositorio? conversacionRepositorio;
    private IProcesamientoInternoMensajeRepositorio? procesamientoInternoMensajeRepositorio;
    private IEnvioMensajeRepositorio? envioMensajeRepositorio;

    public UnitOfWork(MensajeriaContextoDB contexto)
    {
        this.contexto = contexto;
    }

    public IMensajeRepositorio MensajeRepositorio
    {
        get
        {
            return mensajeRepositorio ??= new MensajeRepositorio(contexto);
        }
    }

    public IConversacionRepositorio ConversacionRepositorio
    {
        get
        {
            return conversacionRepositorio ??= new ConversacionRepositorio(contexto);
        }
    }

    public IProcesamientoInternoMensajeRepositorio ProcesamientoInternoMensajeRepositorio
    {
        get
        {
            return procesamientoInternoMensajeRepositorio ??= new ProcesamientoInternoMensajeRepositorio(contexto);
        }
    }

    public IEnvioMensajeRepositorio EnvioMensajeRepositorio
    {
        get
        {
            return envioMensajeRepositorio ??= new EnvioMensajeRepositorio(contexto);
        }
    }

    public Task<int> SaveChangesAsync()
    {
        return contexto.SaveChangesAsync();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (transaccion is not null)
        {
            throw new InvalidOperationException("Ya existe una transaccion activa.");
        }

        transaccion = await contexto.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (transaccion is null)
        {
            throw new InvalidOperationException("No existe una transaccion activa.");
        }

        try
        {
            await transaccion.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaccion.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await transaccion.DisposeAsync();
            transaccion = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (transaccion is null)
        {
            throw new InvalidOperationException("No existe una transaccion activa.");
        }

        try
        {
            await transaccion.RollbackAsync(cancellationToken);
        }
        finally
        {
            await transaccion.DisposeAsync();
            transaccion = null;
        }
    }
}
