using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class RegistrarContextoIAAplicacion : IRegistrarContextoIAAplicacion
{
    private readonly IUnitOfWorkFactory unitOfWorkFactory;

    public RegistrarContextoIAAplicacion(IUnitOfWorkFactory unitOfWorkFactory)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken)
    {
        return await ObtenerMetadataEntradasAsync(
            idLineaConversacion,
            null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasProcesamientoAsync(
        long idLineaConversacion,
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        return await ObtenerMetadataEntradasAsync(
            idLineaConversacion,
            idProcesamientoInternoMensaje,
            cancellationToken);
    }

    private async Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasAsync(
        long idLineaConversacion,
        long? idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        return await (
            from entrada in unitOfWork.MetadataEntradaContextoIARepositorio.GetNoTracking()
            join info in unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.GetNoTracking()
                on entrada.IDInformacionTecnicaLlamadaIA equals (long?)info.ID into informacionTecnicaEntrada
            from info in informacionTecnicaEntrada.DefaultIfEmpty()
            where entrada.IDLineaConversacion == idLineaConversacion
                && (!idProcesamientoInternoMensaje.HasValue
                    || entrada.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje.Value)
            orderby entrada.Orden, entrada.ID
            select new MetadataEntradaContextoIA
            {
                ID = entrada.ID,
                IDLineaConversacion = entrada.IDLineaConversacion,
                IDMensaje = entrada.IDMensaje,
                IDProcesamientoInternoMensaje = entrada.IDProcesamientoInternoMensaje,
                IDInformacionTecnicaLlamadaIA = entrada.IDInformacionTecnicaLlamadaIA,
                IDCompactacionContextoIncorporada = entrada.IDCompactacionContextoIncorporada,
                Orden = entrada.Orden,
                IDRolContextoIA = entrada.IDRolContextoIA,
                IDTipoEntradaContextoIA = entrada.IDTipoEntradaContextoIA,
                Contenido = entrada.Contenido,
                ToolCallID = entrada.ToolCallID,
                FechaEntrada = entrada.FechaEntrada,
                FechaCreacion = entrada.FechaCreacion,
                InformacionTecnicaLlamadaIA = info == null
                    ? null
                    : new InformacionTecnicaLlamadaIAContexto
                    {
                        Proveedor = info.Proveedor,
                        Modelo = info.Modelo,
                        Adaptador = info.Adaptador,
                        Iteracion = info.Iteracion,
                        AccionDecidida = info.AccionDecidida,
                        FinishReason = info.FinishReason,
                        NativeFinishReason = info.NativeFinishReason,
                        PromptTokens = info.PromptTokens,
                        CompletionTokens = info.CompletionTokens,
                        ReasoningTokens = info.ReasoningTokens,
                        TotalTokens = info.TotalTokens,
                        RequestJson = info.RequestJson,
                        ResponseJson = info.ResponseJson,
                        Content = info.Content,
                        Reasoning = info.Reasoning,
                        ReasoningDetailsJson = info.ReasoningDetailsJson,
                        Error = info.Error
                    }
            }).ToListAsync(cancellationToken);
    }

    public async Task<ResultadoRegistrarDecisionContextoIA> RegistrarDecisionAsync(
        SolicitudContextoConversacion solicitud,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        SolicitudRegistrarMetadataEntradaContextoIA entrada,
        SolicitudPrepararEjecucionComandoContexto? preparacionEjecucion,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        DAOInformacionTecnicaLlamadaIALineaConversacion daoInformacionTecnicaLlamadaIA = CrearInformacionTecnicaLlamadaIA(
            solicitud,
            informacionTecnicaLlamadaIA);
        DAOMetadataEntradaContextoIA? daoEntrada = null;
        DAOEjecucionComandoContexto? daoEjecucion = null;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.AgregarAsync(daoInformacionTecnicaLlamadaIA, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            daoEntrada = await CrearMetadataEntradaAsync(
                unitOfWork,
                entrada,
                daoInformacionTecnicaLlamadaIA.ID,
                cancellationToken);
            await unitOfWork.MetadataEntradaContextoIARepositorio.AgregarAsync(daoEntrada, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (preparacionEjecucion is not null)
            {
                daoEjecucion = CrearEjecucionPreparada(solicitud, daoEntrada.ID, preparacionEjecucion);
                await unitOfWork.EjecucionComandoContextoRepositorio.AgregarAsync(daoEjecucion, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            MetadataEntradaContextoIA resultado = MapearMetadataEntrada(daoEntrada);
            resultado.InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA;
            return new ResultadoRegistrarDecisionContextoIA
            {
                MetadataEntradaDecision = resultado,
                EjecucionComando = daoEjecucion is null ? null : MapearEjecucion(daoEjecucion, resultado.ToolCallID)
            };
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
                unitOfWork.MetadataEntradaContextoIARepositorio.LiberarRastreo(daoEntrada);
            }

            if (daoEjecucion is not null)
            {
                unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(daoEjecucion);
            }

            unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.LiberarRastreo(daoInformacionTecnicaLlamadaIA);
        }
    }

    public async Task<MetadataEntradaContextoIA> RegistrarMetadataResultadoComandoAsync(
        long idEjecucionComandoContexto,
        SolicitudRegistrarMetadataEntradaContextoIA entrada,
        ResultadoComandoContexto resultadoComando,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        DAOEjecucionComandoContexto ejecucion = await unitOfWork.EjecucionComandoContextoRepositorio.Get()
            .SingleAsync(ejecucionActual => ejecucionActual.ID == idEjecucionComandoContexto, cancellationToken);
        DAOMetadataEntradaContextoIA? daoEntrada = null;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            daoEntrada = await CrearMetadataEntradaAsync(
                unitOfWork,
                entrada,
                entrada.IDInformacionTecnicaLlamadaIA,
                cancellationToken);
            await unitOfWork.MetadataEntradaContextoIARepositorio.AgregarAsync(daoEntrada, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            ejecucion.IDMetadataEntradaResultadoContextoIA = daoEntrada.ID;
            ejecucion.IDEstadoEjecucionComandoContexto = resultadoComando.Exitoso
                ? EstadosEjecucionComandoContexto.Completada
                : EstadosEjecucionComandoContexto.Fallida;
            ejecucion.Activa = false;
            ejecucion.Error = resultadoComando.Error;
            ejecucion.FechaFinalizacion = DateTime.Now;
            unitOfWork.EjecucionComandoContextoRepositorio.Actualizar(ejecucion);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return MapearMetadataEntrada(daoEntrada);
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
                unitOfWork.MetadataEntradaContextoIARepositorio.LiberarRastreo(daoEntrada);
            }

            unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(ejecucion);
        }
    }

    public async Task<MetadataEntradaContextoIA> RegistrarMetadataEntradaAsync(
        SolicitudRegistrarMetadataEntradaContextoIA solicitud,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        DAOMetadataEntradaContextoIA dao = await CrearMetadataEntradaAsync(
            unitOfWork,
            solicitud,
            solicitud.IDInformacionTecnicaLlamadaIA,
            cancellationToken);

        try
        {
            await unitOfWork.MetadataEntradaContextoIARepositorio.AgregarAsync(dao, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return MapearMetadataEntrada(dao);
        }
        finally
        {
            unitOfWork.MetadataEntradaContextoIARepositorio.LiberarRastreo(dao);
        }
    }

    private async Task<DAOMetadataEntradaContextoIA> CrearMetadataEntradaAsync(
        IUnitOfWork unitOfWork,
        SolicitudRegistrarMetadataEntradaContextoIA solicitud,
        long? idInformacionTecnicaLlamadaIA,
        CancellationToken cancellationToken)
    {
        int ultimoOrden = await unitOfWork.MetadataEntradaContextoIARepositorio.GetNoTracking()
            .Where(entrada => entrada.IDLineaConversacion == solicitud.IDLineaConversacion)
            .Select(entrada => (int?)entrada.Orden)
            .MaxAsync(cancellationToken) ?? 0;

        return new DAOMetadataEntradaContextoIA
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDMensaje = solicitud.IDMensaje,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDInformacionTecnicaLlamadaIA = idInformacionTecnicaLlamadaIA,
            Orden = ultimoOrden + 1,
            IDRolContextoIA = solicitud.IDRolContextoIA,
            IDTipoEntradaContextoIA = solicitud.IDTipoEntradaContextoIA,
            Contenido = solicitud.Contenido,
            ToolCallID = solicitud.ToolCallID,
            FechaEntrada = solicitud.FechaEntrada,
            FechaCreacion = DateTime.Now
        };
    }

    private static DAOInformacionTecnicaLlamadaIALineaConversacion CrearInformacionTecnicaLlamadaIA(
        SolicitudContextoConversacion solicitud,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA)
    {
        return new DAOInformacionTecnicaLlamadaIALineaConversacion
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDMensaje = solicitud.IDMensaje,
            Proveedor = informacionTecnicaLlamadaIA.Proveedor,
            Modelo = informacionTecnicaLlamadaIA.Modelo,
            Adaptador = informacionTecnicaLlamadaIA.Adaptador,
            Iteracion = informacionTecnicaLlamadaIA.Iteracion,
            AccionDecidida = informacionTecnicaLlamadaIA.AccionDecidida,
            FinishReason = informacionTecnicaLlamadaIA.FinishReason,
            NativeFinishReason = informacionTecnicaLlamadaIA.NativeFinishReason,
            PromptTokens = informacionTecnicaLlamadaIA.PromptTokens,
            CompletionTokens = informacionTecnicaLlamadaIA.CompletionTokens,
            ReasoningTokens = informacionTecnicaLlamadaIA.ReasoningTokens,
            TotalTokens = informacionTecnicaLlamadaIA.TotalTokens,
            RequestJson = informacionTecnicaLlamadaIA.RequestJson,
            ResponseJson = informacionTecnicaLlamadaIA.ResponseJson,
            Content = informacionTecnicaLlamadaIA.Content,
            Reasoning = informacionTecnicaLlamadaIA.Reasoning,
            ReasoningDetailsJson = informacionTecnicaLlamadaIA.ReasoningDetailsJson,
            Error = informacionTecnicaLlamadaIA.Error,
            FechaCreacion = DateTime.Now
        };
    }

    private static DAOEjecucionComandoContexto CrearEjecucionPreparada(
        SolicitudContextoConversacion solicitud,
        long idMetadataEntradaDecisionContextoIA,
        SolicitudPrepararEjecucionComandoContexto preparacion)
    {
        return new DAOEjecucionComandoContexto
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDMetadataEntradaDecisionContextoIA = idMetadataEntradaDecisionContextoIA,
            NumeroIntento = 1,
            ProveedorEjecucion = preparacion.ProveedorEjecucion,
            CodigoComando = preparacion.CodigoComando,
            ParametrosJson = preparacion.ParametrosJson,
            IDEstadoEjecucionComandoContexto = EstadosEjecucionComandoContexto.Preparada,
            Activa = true,
            FechaCreacion = DateTime.Now
        };
    }

    private static EjecucionComandoContexto MapearEjecucion(
        DAOEjecucionComandoContexto dao,
        string? toolCallID)
    {
        return new EjecucionComandoContexto
        {
            ID = dao.ID,
            IDEjecucionAnterior = dao.IDEjecucionAnterior,
            IDLineaConversacion = dao.IDLineaConversacion,
            IDProcesamientoInternoMensaje = dao.IDProcesamientoInternoMensaje,
            IDMetadataEntradaDecisionContextoIA = dao.IDMetadataEntradaDecisionContextoIA,
            IDMetadataEntradaResultadoContextoIA = dao.IDMetadataEntradaResultadoContextoIA,
            NumeroIntento = dao.NumeroIntento,
            ProveedorEjecucion = dao.ProveedorEjecucion,
            IdentificadorExterno = dao.IdentificadorExterno,
            CodigoComando = dao.CodigoComando,
            ParametrosJson = dao.ParametrosJson,
            Estado = dao.IDEstadoEjecucionComandoContexto,
            Activa = dao.Activa,
            Error = dao.Error,
            ToolCallID = toolCallID
        };
    }

    private static MetadataEntradaContextoIA MapearMetadataEntrada(DAOMetadataEntradaContextoIA dao)
    {
        return new MetadataEntradaContextoIA
        {
            ID = dao.ID,
            IDLineaConversacion = dao.IDLineaConversacion,
            IDMensaje = dao.IDMensaje,
            IDProcesamientoInternoMensaje = dao.IDProcesamientoInternoMensaje,
            IDInformacionTecnicaLlamadaIA = dao.IDInformacionTecnicaLlamadaIA,
            IDCompactacionContextoIncorporada = dao.IDCompactacionContextoIncorporada,
            Orden = dao.Orden,
            IDRolContextoIA = dao.IDRolContextoIA,
            IDTipoEntradaContextoIA = dao.IDTipoEntradaContextoIA,
            Contenido = dao.Contenido,
            ToolCallID = dao.ToolCallID,
            FechaEntrada = dao.FechaEntrada,
            FechaCreacion = dao.FechaCreacion
        };
    }
}
