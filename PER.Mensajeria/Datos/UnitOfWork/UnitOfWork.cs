using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Repositorio.ArchivoMensaje;
using PER.Mensajeria.Datos.Repositorio.CanalComunicacion;
using PER.Mensajeria.Datos.Repositorio.Conversacion;
using PER.Mensajeria.Datos.Repositorio.ConversacionParticipante;
using PER.Mensajeria.Datos.Repositorio.CuentaCanal;
using PER.Mensajeria.Datos.Repositorio.EnvioMensaje;
using PER.Mensajeria.Datos.Repositorio.LineaConversacion;
using PER.Mensajeria.Datos.Repositorio.Mensaje;
using PER.Mensajeria.Datos.Repositorio.ParticipanteConversacion;
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
    private ICanalComunicacionRepositorio? canalComunicacionRepositorio;
    private ICuentaCanalRepositorio? cuentaCanalRepositorio;
    private IParticipanteConversacionRepositorio? participanteConversacionRepositorio;
    private IConversacionParticipanteRepositorio? conversacionParticipanteRepositorio;
    private ILineaConversacionRepositorio? lineaConversacionRepositorio;
    private IArchivoMensajeRepositorio? archivoMensajeRepositorio;

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

    public ICanalComunicacionRepositorio CanalComunicacionRepositorio
    {
        get
        {
            return canalComunicacionRepositorio ??= new CanalComunicacionRepositorio(contexto);
        }
    }

    public ICuentaCanalRepositorio CuentaCanalRepositorio
    {
        get
        {
            return cuentaCanalRepositorio ??= new CuentaCanalRepositorio(contexto);
        }
    }

    public IParticipanteConversacionRepositorio ParticipanteConversacionRepositorio
    {
        get
        {
            return participanteConversacionRepositorio ??= new ParticipanteConversacionRepositorio(contexto);
        }
    }

    public IConversacionParticipanteRepositorio ConversacionParticipanteRepositorio
    {
        get
        {
            return conversacionParticipanteRepositorio ??= new ConversacionParticipanteRepositorio(contexto);
        }
    }

    public ILineaConversacionRepositorio LineaConversacionRepositorio
    {
        get
        {
            return lineaConversacionRepositorio ??= new LineaConversacionRepositorio(contexto);
        }
    }

    public IArchivoMensajeRepositorio ArchivoMensajeRepositorio
    {
        get
        {
            return archivoMensajeRepositorio ??= new ArchivoMensajeRepositorio(contexto);
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
