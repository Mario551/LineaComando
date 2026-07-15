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
using PER.Mensajeria.Datos.Repositorio;

namespace PER.Mensajeria.Datos.UnitOfWork;

public interface IUnitOfWork
{
    IMensajeRepositorio MensajeRepositorio { get; }
    IConversacionRepositorio ConversacionRepositorio { get; }
    IProcesamientoInternoMensajeRepositorio ProcesamientoInternoMensajeRepositorio { get; }
    IEnvioMensajeRepositorio EnvioMensajeRepositorio { get; }
    ICanalComunicacionRepositorio CanalComunicacionRepositorio => throw new NotImplementedException();
    ICuentaCanalRepositorio CuentaCanalRepositorio => throw new NotImplementedException();
    IParticipanteConversacionRepositorio ParticipanteConversacionRepositorio => throw new NotImplementedException();
    IConversacionParticipanteRepositorio ConversacionParticipanteRepositorio => throw new NotImplementedException();
    ILineaConversacionRepositorio LineaConversacionRepositorio => throw new NotImplementedException();
    IArchivoMensajeRepositorio ArchivoMensajeRepositorio => throw new NotImplementedException();
    IEntradaContextoIARepositorio EntradaContextoIARepositorio => throw new NotImplementedException();
    IMetadataRazonamientoIALineaConversacionRepositorio MetadataRazonamientoIALineaConversacionRepositorio => throw new NotImplementedException();
    IEstadoContextoConversacionRepositorio EstadoContextoConversacionRepositorio => throw new NotImplementedException();

    Task<int> SaveChangesAsync();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}
