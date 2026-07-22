using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

public class EjecucionComandoContextoAplicacion : IEjecucionComandoContextoAplicacion
{
    private const string RolHerramienta = "tool";
    private const string TipoMetadataEntradaResultadoComando = "resultado_comando";

    private readonly IUnitOfWorkFactory unitOfWorkFactory;
    private readonly IEjecutorComandoContextoServicio ejecutorComandoContextoServicio;
    private readonly IRegistrarContextoIAAplicacion registrarContextoIAAplicacion;

    public EjecucionComandoContextoAplicacion(
        IUnitOfWorkFactory unitOfWorkFactory,
        IEjecutorComandoContextoServicio ejecutorComandoContextoServicio,
        IRegistrarContextoIAAplicacion registrarContextoIAAplicacion)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
        this.ejecutorComandoContextoServicio = ejecutorComandoContextoServicio;
        this.registrarContextoIAAplicacion = registrarContextoIAAplicacion;
    }

    public string Proveedor => ejecutorComandoContextoServicio.Proveedor;

    public async Task<ResultadoEjecucionComandoContexto?> ReanudarActivaAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        CancellationToken cancellationToken)
    {
        EjecucionComandoContexto? ejecucion = await ObtenerActivaAsync(
            solicitud.IDProcesamientoInternoMensaje,
            cancellationToken);
        if (ejecucion is null)
        {
            return null;
        }

        if (!string.Equals(ejecucion.ProveedorEjecucion, Proveedor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La ejecucion {ejecucion.ID} pertenece al proveedor '{ejecucion.ProveedorEjecucion}', pero esta registrado '{Proveedor}'.");
        }

        ComandoContexto? comando = comandos.SingleOrDefault(comandoActual =>
            comandoActual.Codigo == ejecucion.CodigoComando && comandoActual.Autorizado);
        if (comando is null)
        {
            return await FinalizarAsync(
                solicitud,
                ejecucion,
                ResultadoComandoContexto.Fallo($"Comando no autorizado al reanudar: {ejecucion.CodigoComando}"),
                cancellationToken);
        }

        IReadOnlyDictionary<string, string> parametros = DeserializarParametros(ejecucion.ParametrosJson);

        if (ejecucion.Estado == EstadosEjecucionComandoContexto.Preparada)
        {
            return await EjecutarAsync(solicitud, ejecucion, comando, parametros, cancellationToken);
        }

        if (ejecucion.Estado is EstadosEjecucionComandoContexto.Encolando or EstadosEjecucionComandoContexto.Incierta)
        {
            EjecucionComandoContexto reintento = await CrearReintentoAsync(
                ejecucion,
                EstadosEjecucionComandoContexto.Incierta,
                "No se pudo confirmar el identificador externo despues del reinicio.",
                cancellationToken);
            return await EjecutarAsync(solicitud, reintento, comando, parametros, cancellationToken);
        }

        if (ejecucion.Estado == EstadosEjecucionComandoContexto.Abandonando)
        {
            ReferenciaEjecucionComandoContexto referenciaAbandono = CrearReferencia(ejecucion);
            await ejecutorComandoContextoServicio.AbandonarAsync(
                referenciaAbandono,
                "Ejecucion interrumpida por reinicio del proceso.",
                cancellationToken);
            EjecucionComandoContexto reintento = await CrearReintentoAsync(
                ejecucion,
                EstadosEjecucionComandoContexto.Abandonada,
                "Ejecucion externa abandonada despues del reinicio.",
                cancellationToken);
            return await EjecutarAsync(solicitud, reintento, comando, parametros, cancellationToken);
        }

        if (ejecucion.Estado != EstadosEjecucionComandoContexto.Encolada)
        {
            throw new InvalidOperationException(
                $"La ejecucion activa {ejecucion.ID} tiene el estado terminal o desconocido '{ejecucion.Estado}'.");
        }

        ReferenciaEjecucionComandoContexto referencia = CrearReferencia(ejecucion);
        ConsultaEjecucionComandoContexto consulta = await ejecutorComandoContextoServicio.ConsultarAsync(
            referencia,
            cancellationToken);

        if (consulta.Estado == EstadoEjecucionComandoExternaContextoTipo.Pendiente)
        {
            return await EsperarYFinalizarAsync(solicitud, ejecucion, referencia, cancellationToken);
        }

        if (consulta.Estado == EstadoEjecucionComandoExternaContextoTipo.Procesando)
        {
            await MarcarEstadoAsync(
                ejecucion.ID,
                EstadosEjecucionComandoContexto.Abandonando,
                null,
                cancellationToken);
            await ejecutorComandoContextoServicio.AbandonarAsync(
                referencia,
                "Ejecucion abandonada porque el proceso que la ejecutaba ya no esta activo.",
                cancellationToken);
            EjecucionComandoContexto reintento = await CrearReintentoAsync(
                ejecucion,
                EstadosEjecucionComandoContexto.Abandonada,
                "Ejecucion procesando abandonada al recuperar el contexto.",
                cancellationToken);
            return await EjecutarAsync(solicitud, reintento, comando, parametros, cancellationToken);
        }

        if (consulta.Estado is EstadoEjecucionComandoExternaContextoTipo.Completado
            or EstadoEjecucionComandoExternaContextoTipo.Fallido)
        {
            return await EsperarYFinalizarAsync(solicitud, ejecucion, referencia, cancellationToken);
        }

        EjecucionComandoContexto intentoNuevo = await CrearReintentoAsync(
            ejecucion,
            EstadosEjecucionComandoContexto.Incierta,
            consulta.Error ?? "El identificador externo no existe.",
            cancellationToken);
        return await EjecutarAsync(solicitud, intentoNuevo, comando, parametros, cancellationToken);
    }

    public async Task<ResultadoEjecucionComandoContexto> EjecutarAsync(
        SolicitudContextoConversacion solicitud,
        EjecucionComandoContexto ejecucion,
        ComandoContexto comando,
        IReadOnlyDictionary<string, string> parametros,
        CancellationToken cancellationToken)
    {
        if (ejecucion.Estado != EstadosEjecucionComandoContexto.Preparada)
        {
            throw new InvalidOperationException(
                $"La ejecucion {ejecucion.ID} debe estar preparada antes de encolar y esta en '{ejecucion.Estado}'.");
        }

        await MarcarEncolandoAsync(ejecucion.ID, cancellationToken);

        ReferenciaEjecucionComandoContexto referencia = await ejecutorComandoContextoServicio.EncolarAsync(
            new SolicitudEjecutarComandoContexto
            {
                Solicitud = solicitud,
                Comando = comando,
                Parametros = parametros
            },
            cancellationToken);

        ValidarReferencia(referencia);
        await MarcarEncoladaAsync(ejecucion.ID, referencia, cancellationToken);
        ejecucion.IdentificadorExterno = referencia.IdentificadorExterno;
        ejecucion.Estado = EstadosEjecucionComandoContexto.Encolada;

        return await EsperarYFinalizarAsync(solicitud, ejecucion, referencia, cancellationToken);
    }

    private async Task<ResultadoEjecucionComandoContexto> EsperarYFinalizarAsync(
        SolicitudContextoConversacion solicitud,
        EjecucionComandoContexto ejecucion,
        ReferenciaEjecucionComandoContexto referencia,
        CancellationToken cancellationToken)
    {
        ResultadoComandoContexto resultado = await ejecutorComandoContextoServicio.EsperarResultadoAsync(
            referencia,
            cancellationToken);
        return await FinalizarAsync(solicitud, ejecucion, resultado, cancellationToken);
    }

    private async Task<ResultadoEjecucionComandoContexto> FinalizarAsync(
        SolicitudContextoConversacion solicitud,
        EjecucionComandoContexto ejecucion,
        ResultadoComandoContexto resultado,
        CancellationToken cancellationToken)
    {
        string contenido = resultado.Exitoso
            ? resultado.Resultado ?? string.Empty
            : JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["error"] = resultado.Error ?? "El comando fallo sin detalle."
            });

        MetadataEntradaContextoIA entrada = await registrarContextoIAAplicacion.RegistrarMetadataResultadoComandoAsync(
            ejecucion.ID,
            new SolicitudRegistrarMetadataEntradaContextoIA
            {
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDRolContextoIA = RolHerramienta,
                IDTipoEntradaContextoIA = TipoMetadataEntradaResultadoComando,
                Contenido = contenido,
                ToolCallID = ejecucion.ToolCallID,
                FechaEntrada = DateTime.Now
            },
            resultado,
            cancellationToken);

        return new ResultadoEjecucionComandoContexto
        {
            Resultado = resultado,
            MetadataEntradaResultado = entrada
        };
    }

    private async Task<EjecucionComandoContexto?> ObtenerActivaAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        return await (
            from ejecucion in unitOfWork.EjecucionComandoContextoRepositorio.GetNoTracking()
            join entrada in unitOfWork.MetadataEntradaContextoIARepositorio.GetNoTracking()
                on ejecucion.IDMetadataEntradaDecisionContextoIA equals entrada.ID
            where ejecucion.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje
                && ejecucion.Activa
            select new EjecucionComandoContexto
            {
                ID = ejecucion.ID,
                IDEjecucionAnterior = ejecucion.IDEjecucionAnterior,
                IDLineaConversacion = ejecucion.IDLineaConversacion,
                IDProcesamientoInternoMensaje = ejecucion.IDProcesamientoInternoMensaje,
                IDMetadataEntradaDecisionContextoIA = ejecucion.IDMetadataEntradaDecisionContextoIA,
                IDMetadataEntradaResultadoContextoIA = ejecucion.IDMetadataEntradaResultadoContextoIA,
                NumeroIntento = ejecucion.NumeroIntento,
                ProveedorEjecucion = ejecucion.ProveedorEjecucion,
                IdentificadorExterno = ejecucion.IdentificadorExterno,
                CodigoComando = ejecucion.CodigoComando,
                ParametrosJson = ejecucion.ParametrosJson,
                Estado = ejecucion.IDEstadoEjecucionComandoContexto,
                Activa = ejecucion.Activa,
                Error = ejecucion.Error,
                ToolCallID = entrada.ToolCallID
            }).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task MarcarEncolandoAsync(long idEjecucion, CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOEjecucionComandoContexto dao = await ObtenerRastreadaAsync(
            unitOfWork,
            idEjecucion,
            cancellationToken);
        try
        {
            dao.IDEstadoEjecucionComandoContexto = EstadosEjecucionComandoContexto.Encolando;
            dao.FechaInicioEncolado = DateTime.Now;
            unitOfWork.EjecucionComandoContextoRepositorio.Actualizar(dao);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(dao);
        }
    }

    private async Task MarcarEncoladaAsync(
        long idEjecucion,
        ReferenciaEjecucionComandoContexto referencia,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOEjecucionComandoContexto dao = await ObtenerRastreadaAsync(
            unitOfWork,
            idEjecucion,
            cancellationToken);
        try
        {
            dao.ProveedorEjecucion = referencia.Proveedor;
            dao.IdentificadorExterno = referencia.IdentificadorExterno;
            dao.IDEstadoEjecucionComandoContexto = EstadosEjecucionComandoContexto.Encolada;
            dao.FechaEncolado = DateTime.Now;
            unitOfWork.EjecucionComandoContextoRepositorio.Actualizar(dao);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(dao);
        }
    }

    private async Task MarcarEstadoAsync(
        long idEjecucion,
        string estado,
        string? error,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOEjecucionComandoContexto dao = await ObtenerRastreadaAsync(
            unitOfWork,
            idEjecucion,
            cancellationToken);
        try
        {
            dao.IDEstadoEjecucionComandoContexto = estado;
            dao.Error = error;
            unitOfWork.EjecucionComandoContextoRepositorio.Actualizar(dao);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(dao);
        }
    }

    private async Task<EjecucionComandoContexto> CrearReintentoAsync(
        EjecucionComandoContexto ejecucionAnterior,
        string estadoAnterior,
        string motivo,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOEjecucionComandoContexto anterior = await ObtenerRastreadaAsync(
            unitOfWork,
            ejecucionAnterior.ID,
            cancellationToken);
        DAOEjecucionComandoContexto? nuevo = null;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            anterior.IDEstadoEjecucionComandoContexto = estadoAnterior;
            anterior.Activa = false;
            anterior.Error = motivo;
            anterior.FechaFinalizacion = DateTime.Now;
            unitOfWork.EjecucionComandoContextoRepositorio.Actualizar(anterior);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            nuevo = new DAOEjecucionComandoContexto
            {
                IDEjecucionAnterior = anterior.ID,
                IDLineaConversacion = anterior.IDLineaConversacion,
                IDProcesamientoInternoMensaje = anterior.IDProcesamientoInternoMensaje,
                IDMetadataEntradaDecisionContextoIA = anterior.IDMetadataEntradaDecisionContextoIA,
                NumeroIntento = anterior.NumeroIntento + 1,
                ProveedorEjecucion = anterior.ProveedorEjecucion,
                CodigoComando = anterior.CodigoComando,
                ParametrosJson = anterior.ParametrosJson,
                IDEstadoEjecucionComandoContexto = EstadosEjecucionComandoContexto.Preparada,
                Activa = true,
                FechaCreacion = DateTime.Now
            };
            await unitOfWork.EjecucionComandoContextoRepositorio.AgregarAsync(nuevo, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return Mapear(nuevo, ejecucionAnterior.ToolCallID);
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
            unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(anterior);
            if (nuevo is not null)
            {
                unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(nuevo);
            }
        }
    }

    private async Task<DAOEjecucionComandoContexto> ObtenerRastreadaAsync(
        IUnitOfWork unitOfWork,
        long idEjecucion,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.EjecucionComandoContextoRepositorio.Get()
            .SingleAsync(ejecucion => ejecucion.ID == idEjecucion, cancellationToken);
    }

    private static ReferenciaEjecucionComandoContexto CrearReferencia(EjecucionComandoContexto ejecucion)
    {
        if (string.IsNullOrWhiteSpace(ejecucion.IdentificadorExterno))
        {
            throw new InvalidOperationException(
                $"La ejecucion {ejecucion.ID} no tiene identificador externo para consultar.");
        }

        return new ReferenciaEjecucionComandoContexto
        {
            Proveedor = ejecucion.ProveedorEjecucion,
            IdentificadorExterno = ejecucion.IdentificadorExterno
        };
    }

    private static void ValidarReferencia(ReferenciaEjecucionComandoContexto referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia.Proveedor))
        {
            throw new InvalidOperationException("El ejecutor no devolvio proveedor de ejecucion.");
        }

        if (string.IsNullOrWhiteSpace(referencia.IdentificadorExterno))
        {
            throw new InvalidOperationException("El ejecutor no devolvio identificador externo.");
        }
    }

    private static IReadOnlyDictionary<string, string> DeserializarParametros(string parametrosJson)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(parametrosJson)
            ?? throw new InvalidOperationException("Los parametros persistidos del comando no son validos.");
    }

    private static EjecucionComandoContexto Mapear(
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
}
