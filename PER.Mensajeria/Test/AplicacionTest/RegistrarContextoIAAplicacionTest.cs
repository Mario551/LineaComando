using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class RegistrarContextoIAAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task ObtenerMetadataEntradasAsync_DebeCargarTodaLaLineaConInformacionTecnicaEnOrdenSinRastreo(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        (DAOMensaje mensajeMismaLinea, DAOProcesamientoInternoMensaje procesamientoMismaLinea) =
            await CrearMensajeAsync(baseDatos, mensaje.IDLineaConversacion, "segundo");
        (DAOMensaje mensajeOtraLinea, DAOProcesamientoInternoMensaje procesamientoOtraLinea) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        RegistrarContextoIAAplicacion aplicacion = new(new UnitOfWorkFactoryPrueba(baseDatos));

        await aplicacion.RegistrarMetadataEntradaAsync(
            CrearEntrada(mensaje, procesamiento, "user", "mensaje_entrada", "pregunta"),
            CancellationToken.None);
        ResultadoRegistrarDecisionContextoIA decision = await aplicacion.RegistrarDecisionAsync(
            CrearSolicitud(mensaje, procesamiento),
            CrearInformacionTecnicaLlamadaIA(),
            CrearEntrada(mensaje, procesamiento, "assistant", "decision_comando", "decision"),
            null,
            CancellationToken.None);
        await aplicacion.RegistrarMetadataEntradaAsync(
            CrearEntrada(mensaje, procesamiento, "tool", "resultado_comando", "resultado"),
            CancellationToken.None);
        await aplicacion.RegistrarMetadataEntradaAsync(
            CrearEntrada(mensajeMismaLinea, procesamientoMismaLinea, "user", "mensaje_entrada", "segunda pregunta"),
            CancellationToken.None);
        await aplicacion.RegistrarMetadataEntradaAsync(
            CrearEntrada(mensajeOtraLinea, procesamientoOtraLinea, "user", "mensaje_entrada", "otra linea"),
            CancellationToken.None);

        IReadOnlyList<MetadataEntradaContextoIA> entradas = await aplicacion.ObtenerMetadataEntradasAsync(
            mensaje.IDLineaConversacion,
            CancellationToken.None);
        IReadOnlyList<MetadataEntradaContextoIA> entradasPrimerProcesamiento =
            await aplicacion.ObtenerMetadataEntradasProcesamientoAsync(
                mensaje.IDLineaConversacion,
                procesamiento.ID,
                CancellationToken.None);

        Assert.Equal(4, entradas.Count);
        Assert.Equal([1, 2, 3, 4], entradas.Select(entrada => entrada.Orden));
        Assert.All(entradas, entrada => Assert.Equal(mensaje.IDLineaConversacion, entrada.IDLineaConversacion));
        Assert.Equal(3, entradasPrimerProcesamiento.Count);
        Assert.All(
            entradasPrimerProcesamiento,
            entrada => Assert.Equal(procesamiento.ID, entrada.IDProcesamientoInternoMensaje));
        Assert.All(entradas, entrada => Assert.NotEqual(default, entrada.FechaCreacion));
        MetadataEntradaContextoIA metadataEntradaDecision = Assert.Single(
            entradas,
            entrada => entrada.ID == decision.MetadataEntradaDecision.ID);
        Assert.NotNull(metadataEntradaDecision.InformacionTecnicaLlamadaIA);
        Assert.Equal("razonamiento de prueba", metadataEntradaDecision.InformacionTecnicaLlamadaIA.Reasoning);
        Assert.Equal("finish", metadataEntradaDecision.InformacionTecnicaLlamadaIA.FinishReason);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarDecisionAsync_EntradaInvalida_DebeRevertirInformacionTecnicaYLiberarRastreo(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        RegistrarContextoIAAplicacion aplicacion = new(new UnitOfWorkFactoryPrueba(baseDatos));
        SolicitudRegistrarMetadataEntradaContextoIA entrada = CrearEntrada(
            mensaje,
            procesamiento,
            "rol_inexistente",
            "decision_comando",
            "decision invalida");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => aplicacion.RegistrarDecisionAsync(
                CrearSolicitud(mensaje, procesamiento),
                CrearInformacionTecnicaLlamadaIA(),
                entrada,
                null,
                CancellationToken.None));

        Assert.Empty(contexto.ChangeTracker.Entries());
        Assert.Equal(
            0,
            await contexto.InformacionTecnicaLlamadasIALineaConversacion.AsNoTracking().CountAsync());
        Assert.Equal(
            0,
            await contexto.MetadataEntradasContextoIA.AsNoTracking().CountAsync());
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarDecisionAsync_ConEjecucion_DebeCrearYCerrarIntentoAtomicamente(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        RegistrarContextoIAAplicacion aplicacion = new(new UnitOfWorkFactoryPrueba(baseDatos));

        ResultadoRegistrarDecisionContextoIA registro = await aplicacion.RegistrarDecisionAsync(
            CrearSolicitud(mensaje, procesamiento),
            CrearInformacionTecnicaLlamadaIA(),
            CrearEntrada(mensaje, procesamiento, "assistant", "decision_comando", "decision"),
            new SolicitudPrepararEjecucionComandoContexto
            {
                ProveedorEjecucion = "lineacomando",
                CodigoComando = "pedido consultar",
                ParametrosJson = "{\"pedido\":\"54013\"}"
            },
            CancellationToken.None);

        EjecucionComandoContexto ejecucion = Assert.IsType<EjecucionComandoContexto>(registro.EjecucionComando);
        Assert.Equal(EstadosEjecucionComandoContexto.Preparada, ejecucion.Estado);
        Assert.True(ejecucion.Activa);

        MetadataEntradaContextoIA metadataEntradaResultado = await aplicacion.RegistrarMetadataResultadoComandoAsync(
            ejecucion.ID,
            CrearEntrada(mensaje, procesamiento, "tool", "resultado_comando", "pedido encontrado"),
            ResultadoComandoContexto.Exito("pedido encontrado"),
            CancellationToken.None);

        DAOEjecucionComandoContexto dao = await contexto.EjecucionesComandoContexto.AsNoTracking().SingleAsync();
        Assert.Equal(EstadosEjecucionComandoContexto.Completada, dao.IDEstadoEjecucionComandoContexto);
        Assert.False(dao.Activa);
        Assert.Equal(metadataEntradaResultado.ID, dao.IDMetadataEntradaResultadoContextoIA);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarDecisionAsync_EjecucionInvalida_DebeRevertirDecisionCompleta(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        RegistrarContextoIAAplicacion aplicacion = new(new UnitOfWorkFactoryPrueba(baseDatos));

        await Assert.ThrowsAsync<DbUpdateException>(() => aplicacion.RegistrarDecisionAsync(
            CrearSolicitud(mensaje, procesamiento),
            CrearInformacionTecnicaLlamadaIA(),
            CrearEntrada(mensaje, procesamiento, "assistant", "decision_comando", "decision"),
            new SolicitudPrepararEjecucionComandoContexto
            {
                ProveedorEjecucion = new string('x', 65),
                CodigoComando = "pedido consultar",
                ParametrosJson = "{}"
            },
            CancellationToken.None));

        Assert.Equal(0, await contexto.InformacionTecnicaLlamadasIALineaConversacion.AsNoTracking().CountAsync());
        Assert.Equal(0, await contexto.MetadataEntradasContextoIA.AsNoTracking().CountAsync());
        Assert.Equal(0, await contexto.EjecucionesComandoContexto.AsNoTracking().CountAsync());
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    private static async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearMensajeAsync(
        BaseDatosPrueba baseDatos,
        long idLineaConversacion,
        string sufijo)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DateTime fecha = DateTime.Now;
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = idLineaConversacion,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            Contenido = $"mensaje {sufijo}",
            IdentificadorExternoMensaje = $"contexto_{sufijo}_{Guid.NewGuid():N}",
            FechaMensaje = fecha,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };
        contexto.Mensajes.Add(mensaje);
        await contexto.SaveChangesAsync();

        DAOProcesamientoInternoMensaje procesamiento = new()
        {
            IDMensaje = mensaje.ID,
            IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
            IDEstadoProcesamientoInternoMensaje = "pendiente",
            FechaCreacion = fecha
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();
        return (mensaje, procesamiento);
    }

    private static SolicitudContextoConversacion CrearSolicitud(
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        return new SolicitudContextoConversacion
        {
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMensaje = mensaje.ID,
            IDLineaConversacion = mensaje.IDLineaConversacion,
            TipoMensaje = mensaje.IDTipoMensaje,
            Contenido = mensaje.Contenido,
            FechaMensaje = mensaje.FechaMensaje
        };
    }

    private static SolicitudRegistrarMetadataEntradaContextoIA CrearEntrada(
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento,
        string rol,
        string tipo,
        string contenido)
    {
        return new SolicitudRegistrarMetadataEntradaContextoIA
        {
            IDLineaConversacion = mensaje.IDLineaConversacion,
            IDMensaje = mensaje.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDRolContextoIA = rol,
            IDTipoEntradaContextoIA = tipo,
            Contenido = contenido,
            FechaEntrada = mensaje.FechaMensaje
        };
    }

    private static InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaLlamadaIA()
    {
        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = "proveedor_prueba",
            Modelo = "modelo_prueba",
            Adaptador = "adaptador_prueba",
            Iteracion = 1,
            AccionDecidida = nameof(AccionContextoTipo.Comando),
            FinishReason = "finish",
            RequestJson = "{}",
            ResponseJson = "{}",
            Content = "decision",
            Reasoning = "razonamiento de prueba"
        };
    }
}
