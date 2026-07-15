using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class RegistrarContextoIAAplicacion : IRegistrarContextoIAAplicacion
{
    private readonly IUnitOfWork unitOfWork;

    public RegistrarContextoIAAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<EntradaContextoIA>> ObtenerEntradasAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken)
    {
        return await (
            from entrada in unitOfWork.EntradaContextoIARepositorio.GetNoTracking()
            join metadata in unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.GetNoTracking()
                on entrada.IDMetadataRazonamientoIA equals (long?)metadata.ID into metadataEntrada
            from metadata in metadataEntrada.DefaultIfEmpty()
            where entrada.IDLineaConversacion == idLineaConversacion
            orderby entrada.Orden, entrada.ID
            select new EntradaContextoIA
            {
                ID = entrada.ID,
                IDLineaConversacion = entrada.IDLineaConversacion,
                IDMensaje = entrada.IDMensaje,
                IDProcesamientoInternoMensaje = entrada.IDProcesamientoInternoMensaje,
                IDMetadataRazonamientoIA = entrada.IDMetadataRazonamientoIA,
                Orden = entrada.Orden,
                IDRolContextoIA = entrada.IDRolContextoIA,
                IDTipoEntradaContextoIA = entrada.IDTipoEntradaContextoIA,
                Contenido = entrada.Contenido,
                ToolCallID = entrada.ToolCallID,
                FechaEntrada = entrada.FechaEntrada,
                Metadata = metadata == null
                    ? null
                    : new MetadataRazonamientoIAContexto
                    {
                        Proveedor = metadata.Proveedor,
                        Modelo = metadata.Modelo,
                        Adaptador = metadata.Adaptador,
                        Iteracion = metadata.Iteracion,
                        AccionDecidida = metadata.AccionDecidida,
                        FinishReason = metadata.FinishReason,
                        NativeFinishReason = metadata.NativeFinishReason,
                        PromptTokens = metadata.PromptTokens,
                        CompletionTokens = metadata.CompletionTokens,
                        ReasoningTokens = metadata.ReasoningTokens,
                        TotalTokens = metadata.TotalTokens,
                        RequestJson = metadata.RequestJson,
                        ResponseJson = metadata.ResponseJson,
                        Content = metadata.Content,
                        Reasoning = metadata.Reasoning,
                        ReasoningDetailsJson = metadata.ReasoningDetailsJson,
                        Error = metadata.Error
                    }
            }).ToListAsync(cancellationToken);
    }

    public async Task<EntradaContextoIA> RegistrarDecisionAsync(
        SolicitudContextoConversacion solicitud,
        MetadataRazonamientoIAContexto metadata,
        SolicitudRegistrarEntradaContextoIA entrada,
        CancellationToken cancellationToken)
    {
        DAOMetadataRazonamientoIALineaConversacion daoMetadata = CrearMetadata(solicitud, metadata);
        DAOEntradaContextoIA? daoEntrada = null;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.AgregarAsync(daoMetadata, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            daoEntrada = await CrearEntradaAsync(entrada, daoMetadata.ID, cancellationToken);
            await unitOfWork.EntradaContextoIARepositorio.AgregarAsync(daoEntrada, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            EntradaContextoIA resultado = MapearEntrada(daoEntrada);
            resultado.Metadata = metadata;
            return resultado;
        }
        catch
        {
            try
            {
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
        finally
        {
            if (daoEntrada is not null)
            {
                unitOfWork.EntradaContextoIARepositorio.LiberarRastreo(daoEntrada);
            }

            unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.LiberarRastreo(daoMetadata);
        }
    }

    public async Task<EntradaContextoIA> RegistrarEntradaAsync(
        SolicitudRegistrarEntradaContextoIA solicitud,
        CancellationToken cancellationToken)
    {
        DAOEntradaContextoIA dao = await CrearEntradaAsync(
            solicitud,
            solicitud.IDMetadataRazonamientoIA,
            cancellationToken);

        try
        {
            await unitOfWork.EntradaContextoIARepositorio.AgregarAsync(dao, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return MapearEntrada(dao);
        }
        finally
        {
            unitOfWork.EntradaContextoIARepositorio.LiberarRastreo(dao);
        }
    }

    private async Task<DAOEntradaContextoIA> CrearEntradaAsync(
        SolicitudRegistrarEntradaContextoIA solicitud,
        long? idMetadataRazonamientoIA,
        CancellationToken cancellationToken)
    {
        int ultimoOrden = await unitOfWork.EntradaContextoIARepositorio.GetNoTracking()
            .Where(entrada => entrada.IDLineaConversacion == solicitud.IDLineaConversacion)
            .Select(entrada => (int?)entrada.Orden)
            .MaxAsync(cancellationToken) ?? 0;

        return new DAOEntradaContextoIA
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDMensaje = solicitud.IDMensaje,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDMetadataRazonamientoIA = idMetadataRazonamientoIA,
            Orden = ultimoOrden + 1,
            IDRolContextoIA = solicitud.IDRolContextoIA,
            IDTipoEntradaContextoIA = solicitud.IDTipoEntradaContextoIA,
            Contenido = solicitud.Contenido,
            ToolCallID = solicitud.ToolCallID,
            FechaEntrada = solicitud.FechaEntrada,
            FechaCreacion = DateTime.Now
        };
    }

    private static DAOMetadataRazonamientoIALineaConversacion CrearMetadata(
        SolicitudContextoConversacion solicitud,
        MetadataRazonamientoIAContexto metadata)
    {
        return new DAOMetadataRazonamientoIALineaConversacion
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDMensaje = solicitud.IDMensaje,
            Proveedor = metadata.Proveedor,
            Modelo = metadata.Modelo,
            Adaptador = metadata.Adaptador,
            Iteracion = metadata.Iteracion,
            AccionDecidida = metadata.AccionDecidida,
            FinishReason = metadata.FinishReason,
            NativeFinishReason = metadata.NativeFinishReason,
            PromptTokens = metadata.PromptTokens,
            CompletionTokens = metadata.CompletionTokens,
            ReasoningTokens = metadata.ReasoningTokens,
            TotalTokens = metadata.TotalTokens,
            RequestJson = metadata.RequestJson,
            ResponseJson = metadata.ResponseJson,
            Content = metadata.Content,
            Reasoning = metadata.Reasoning,
            ReasoningDetailsJson = metadata.ReasoningDetailsJson,
            Error = metadata.Error,
            FechaCreacion = DateTime.Now
        };
    }

    private static EntradaContextoIA MapearEntrada(DAOEntradaContextoIA dao)
    {
        return new EntradaContextoIA
        {
            ID = dao.ID,
            IDLineaConversacion = dao.IDLineaConversacion,
            IDMensaje = dao.IDMensaje,
            IDProcesamientoInternoMensaje = dao.IDProcesamientoInternoMensaje,
            IDMetadataRazonamientoIA = dao.IDMetadataRazonamientoIA,
            Orden = dao.Orden,
            IDRolContextoIA = dao.IDRolContextoIA,
            IDTipoEntradaContextoIA = dao.IDTipoEntradaContextoIA,
            Contenido = dao.Contenido,
            ToolCallID = dao.ToolCallID,
            FechaEntrada = dao.FechaEntrada
        };
    }
}
