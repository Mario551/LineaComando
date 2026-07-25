using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class RenovarLineaContextoAplicacionTest
{
    public static IEnumerable<object[]> Motores => BaseDatosPrueba.Motores;

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task EjecutarAsync_DebeRenovarLineaMoverProcesamientoActualYSerIdempotente(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        DatosRenovacion datos = await PrepararDatosRenovacionAsync(baseDatos, mensaje, procesamiento);
        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        IRenovarLineaContextoAplicacion aplicacion = new RenovarLineaContextoAplicacion(unitOfWorkFactory);
        SolicitudRenovarLineaContexto solicitud = CrearSolicitud(datos, "snapshot v1");

        ResultadoRenovarLineaContexto resultado = await aplicacion.EjecutarAsync(
            solicitud,
            CancellationToken.None);
        ResultadoRenovarLineaContexto reintento = await aplicacion.EjecutarAsync(
            solicitud,
            CancellationToken.None);
        ICompactacionContextoConversacionAplicacion compactacionAplicacion = new CompactacionContextoConversacionAplicacion(
            unitOfWorkFactory);
        CompactacionContextoConversacion? compactacionInicial = await compactacionAplicacion.ObtenerInicialAsync(
            resultado.IDLineaConversacion,
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        List<DAOLineaConversacion> lineas = await verificacion.LineasConversacion
            .Where(linea => linea.IDConversacion == datos.IDConversacion)
            .OrderBy(linea => linea.ID)
            .ToListAsync();
        DAOCompactacionContextoConversacion compactacion = await verificacion.CompactacionesContextoConversacion.SingleAsync();
        DAOMensaje mensajeActual = await verificacion.Mensajes.SingleAsync(
            mensajeActual => mensajeActual.ID == datos.IDMensaje);
        DAOProcesamientoInternoMensaje procesamientoActual = await verificacion.ProcesamientosInternosMensaje.SingleAsync(
            procesamientoActual => procesamientoActual.ID == datos.IDProcesamiento);
        List<DAOMetadataEntradaContextoIA> entradasLineaAnterior = await verificacion.MetadataEntradasContextoIA
            .Where(entrada => entrada.IDLineaConversacion == datos.IDLineaOrigen)
            .OrderBy(entrada => entrada.Orden)
            .ToListAsync();
        List<DAOMetadataEntradaContextoIA> entradasLineaNueva = await verificacion.MetadataEntradasContextoIA
            .Where(entrada => entrada.IDLineaConversacion == resultado.IDLineaConversacion)
            .OrderBy(entrada => entrada.Orden)
            .ToListAsync();
        List<DAOInformacionTecnicaLlamadaIALineaConversacion> metadataLineaNueva = await verificacion.InformacionTecnicaLlamadasIALineaConversacion
            .Where(metadata => metadata.IDLineaConversacion == resultado.IDLineaConversacion)
            .ToListAsync();
        List<DAOInformacionTecnicaLlamadaIALineaConversacion> informacionTecnicaCompactacion = await verificacion.InformacionTecnicaLlamadasIALineaConversacion
            .Where(metadata => metadata.IDLineaConversacion == datos.IDLineaOrigen
                && metadata.AccionDecidida == "Compactar")
            .OrderBy(metadata => metadata.ID)
            .ToListAsync();
        List<DAOEjecucionComandoContexto> ejecucionesComando = await verificacion.EjecucionesComandoContexto
            .Where(ejecucion => ejecucion.IDProcesamientoInternoMensaje == datos.IDProcesamiento)
            .OrderBy(ejecucion => ejecucion.NumeroIntento)
            .ToListAsync();

        Assert.Equal(2, lineas.Count);
        Assert.False(lineas.Single(linea => linea.ID == datos.IDLineaOrigen).Activa);
        DAOLineaConversacion lineaNueva = lineas.Single(linea => linea.ID == resultado.IDLineaConversacion);
        Assert.True(lineaNueva.Activa);
        Assert.Equal(datos.IDConversacion, lineaNueva.IDConversacion);
        Assert.Equal(compactacion.ID, lineaNueva.IDCompactacionContextoInicial);
        Assert.Equal(datos.IDLineaOrigen, compactacion.IDLineaConversacionOrigen);
        Assert.Equal(1, compactacion.Version);
        Assert.Equal("snapshot v1", compactacion.Contenido);
        Assert.NotNull(compactacionInicial);
        Assert.Equal(compactacion.ID, compactacionInicial.ID);
        Assert.Equal("snapshot v1", compactacionInicial.Contenido);
        Assert.Equal(lineaNueva.ID, mensajeActual.IDLineaConversacion);
        Assert.Equal("pendiente", procesamientoActual.IDEstadoProcesamientoInternoMensaje);
        Assert.Null(procesamientoActual.Error);
        Assert.Equal(["mensaje_entrada", "limite_ventana"], entradasLineaAnterior.Select(entrada => entrada.IDTipoEntradaContextoIA));
        Assert.Equal(
            [
                "mensaje_entrada",
                "decision_comando",
                "resultado_comando",
                "decision_consulta_mensajes_linea_anterior",
                "resultado_consulta_mensajes_linea_anterior"
            ],
            entradasLineaNueva.Select(entrada => entrada.IDTipoEntradaContextoIA));
        Assert.Equal([1, 2, 3, 4, 5], entradasLineaNueva.Select(entrada => entrada.Orden));
        Assert.Contains(entradasLineaNueva, entrada => entrada.IDTipoEntradaContextoIA == "resultado_comando");
        DAOMetadataEntradaContextoIA resultadoConsulta = Assert.Single(
            entradasLineaNueva,
            entrada => entrada.IDTipoEntradaContextoIA == "resultado_consulta_mensajes_linea_anterior");
        Assert.Equal(compactacion.ID, resultadoConsulta.IDCompactacionContextoIncorporada);
        DAOInformacionTecnicaLlamadaIALineaConversacion metadataComando = Assert.Single(
            metadataLineaNueva,
            metadata => metadata.AccionDecidida == "Comando");
        Assert.Equal(datos.IDMetadataComando, metadataComando.ID);
        Assert.Contains(metadataLineaNueva, metadata => metadata.AccionDecidida == "ConsultarMensajesLineaAnterior");
        Assert.Equal(2, informacionTecnicaCompactacion.Count);
        Assert.Equal(informacionTecnicaCompactacion[^1].ID, compactacion.IDInformacionTecnicaLlamadaIA);
        Assert.Equal("compactacion final", informacionTecnicaCompactacion[^1].Content);
        Assert.Equal(2, ejecucionesComando.Count);
        Assert.All(ejecucionesComando, ejecucion => Assert.Equal(lineaNueva.ID, ejecucion.IDLineaConversacion));
        Assert.Equal(ejecucionesComando[0].ID, ejecucionesComando[1].IDEjecucionAnterior);
        Assert.Equal([1, 2], ejecucionesComando.Select(ejecucion => ejecucion.NumeroIntento));
        Assert.Equal(resultado.IDCompactacionContexto, reintento.IDCompactacionContexto);
        Assert.Equal(resultado.IDLineaConversacion, reintento.IDLineaConversacion);
        Assert.Single(await verificacion.CompactacionesContextoConversacion.ToListAsync());
        Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
        Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task EjecutarAsync_LoteMensajes_DebeMoverTodosLosMensajesYProcesamientos(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje primerMensaje, DAOProcesamientoInternoMensaje primerProcesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        DatosRenovacion datos = await PrepararDatosRenovacionAsync(
            baseDatos,
            primerMensaje,
            primerProcesamiento);
        (DAOMensaje segundoMensaje, DAOProcesamientoInternoMensaje segundoProcesamiento) =
            await CrearSegundoMensajeLoteAsync(
                baseDatos,
                datos.IDLineaOrigen,
                primerMensaje.FechaMensaje.AddSeconds(1));
        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        IRenovarLineaContextoAplicacion aplicacion =
            new RenovarLineaContextoAplicacion(unitOfWorkFactory);
        SolicitudRenovarLineaContexto solicitud = CrearSolicitud(datos, "snapshot lote");
        solicitud.IDsMensajes = [primerMensaje.ID, segundoMensaje.ID];
        solicitud.IDsProcesamientosInternosMensaje =
            [primerProcesamiento.ID, segundoProcesamiento.ID];

        ResultadoRenovarLineaContexto resultado = await aplicacion.EjecutarAsync(
            solicitud,
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        List<DAOMensaje> mensajes = await verificacion.Mensajes
            .Where(mensaje =>
                mensaje.ID == primerMensaje.ID
                || mensaje.ID == segundoMensaje.ID)
            .OrderBy(mensaje => mensaje.ID)
            .ToListAsync();
        List<DAOProcesamientoInternoMensaje> procesamientos =
            await verificacion.ProcesamientosInternosMensaje
                .Where(procesamiento =>
                    procesamiento.ID == primerProcesamiento.ID
                    || procesamiento.ID == segundoProcesamiento.ID)
                .OrderBy(procesamiento => procesamiento.ID)
                .ToListAsync();
        DAOMetadataEntradaContextoIA entradaSegundoMensaje =
            await verificacion.MetadataEntradasContextoIA.SingleAsync(
                entrada => entrada.IDMensaje == segundoMensaje.ID);

        Assert.All(
            mensajes,
            mensaje => Assert.Equal(resultado.IDLineaConversacion, mensaje.IDLineaConversacion));
        Assert.All(
            procesamientos,
            procesamiento => Assert.Equal(
                "pendiente",
                procesamiento.IDEstadoProcesamientoInternoMensaje));
        Assert.Equal(resultado.IDLineaConversacion, entradaSegundoMensaje.IDLineaConversacion);
        Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
        Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task EjecutarAsync_SegundaCompactacion_DebeCrearVersionAcumulativa(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje1, DAOProcesamientoInternoMensaje procesamiento1) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        DatosRenovacion datos1 = await PrepararDatosRenovacionAsync(baseDatos, mensaje1, procesamiento1);
        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        IRenovarLineaContextoAplicacion aplicacion = new RenovarLineaContextoAplicacion(unitOfWorkFactory);
        ResultadoRenovarLineaContexto renovacion1 = await aplicacion.EjecutarAsync(
            CrearSolicitud(datos1, "snapshot v1"),
            CancellationToken.None);
        DatosRenovacion datos2 = await PrepararSegundoProcesamientoAsync(baseDatos, renovacion1);

        ResultadoRenovarLineaContexto renovacion2 = await aplicacion.EjecutarAsync(
            CrearSolicitud(datos2, "snapshot v2"),
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        List<DAOCompactacionContextoConversacion> compactaciones = await verificacion.CompactacionesContextoConversacion
            .OrderBy(compactacion => compactacion.Version)
            .ToListAsync();
        DAOCompactacionContextoConversacion compactacion1 = compactaciones[0];
        DAOCompactacionContextoConversacion compactacion2 = compactaciones[1];
        DAOLineaConversacion lineaFinal = await verificacion.LineasConversacion.SingleAsync(
            linea => linea.ID == renovacion2.IDLineaConversacion);
        int entradasPrimerProcesamientoEnLineaIntermedia = await verificacion.MetadataEntradasContextoIA.CountAsync(
            entrada => entrada.IDLineaConversacion == renovacion1.IDLineaConversacion
                && entrada.IDProcesamientoInternoMensaje == datos1.IDProcesamiento);

        Assert.Equal(2, compactaciones.Count);
        Assert.Equal(1, compactacion1.Version);
        Assert.Equal(2, compactacion2.Version);
        Assert.Equal(compactacion1.ID, compactacion2.IDCompactacionContextoAnterior);
        Assert.Equal("snapshot v2", compactacion2.Contenido);
        Assert.Equal(compactacion2.ID, lineaFinal.IDCompactacionContextoInicial);
        Assert.Equal(5, entradasPrimerProcesamientoEnLineaIntermedia);
        Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
        Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
    }

    private static async Task<DatosRenovacion> PrepararDatosRenovacionAsync(
        BaseDatosPrueba baseDatos,
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOLineaConversacion linea = await contexto.LineasConversacion.SingleAsync(
            lineaActual => lineaActual.ID == mensaje.IDLineaConversacion);
        procesamiento.IDEstadoProcesamientoInternoMensaje = "en_proceso";
        contexto.ProcesamientosInternosMensaje.Update(procesamiento);

        DAOInformacionTecnicaLlamadaIALineaConversacion metadataComando = CrearInformacionTecnicaLlamadaIA(
            linea.ID,
            procesamiento.ID,
            mensaje.ID,
            1,
            "Comando");
        DAOInformacionTecnicaLlamadaIALineaConversacion metadataLimite = CrearInformacionTecnicaLlamadaIA(
            linea.ID,
            procesamiento.ID,
            mensaje.ID,
            3,
            "LimiteVentanaAlcanzado");
        DAOInformacionTecnicaLlamadaIALineaConversacion metadataConsulta = CrearInformacionTecnicaLlamadaIA(
            linea.ID,
            procesamiento.ID,
            mensaje.ID,
            2,
            "ConsultarMensajesLineaAnterior");
        contexto.InformacionTecnicaLlamadasIALineaConversacion.AddRange(
            metadataComando,
            metadataConsulta,
            metadataLimite);
        await contexto.SaveChangesAsync();

        DateTime fecha = DateTime.Now;
        DAOMetadataEntradaContextoIA metadataEntradaDecision = CrearEntrada(
            linea.ID,
            mensaje.ID,
            procesamiento.ID,
            metadataComando.ID,
            3,
            "assistant",
            "decision_comando",
            "ejecutar comando",
            fecha.AddMinutes(-3));
        DAOMetadataEntradaContextoIA metadataEntradaResultado = CrearEntrada(
            linea.ID,
            mensaje.ID,
            procesamiento.ID,
            null,
            4,
            "tool",
            "resultado_comando",
            "comando completado",
            fecha.AddMinutes(-2));
        DAOMetadataEntradaContextoIA metadataEntradaDecisionConsulta = CrearEntrada(
            linea.ID,
            mensaje.ID,
            procesamiento.ID,
            metadataConsulta.ID,
            5,
            "assistant",
            "decision_consulta_mensajes_linea_anterior",
            "consultar ciclo anterior",
            fecha.AddMinutes(-1));
        DAOMetadataEntradaContextoIA metadataEntradaResultadoConsulta = CrearEntrada(
            linea.ID,
            mensaje.ID,
            procesamiento.ID,
            null,
            6,
            "tool",
            "resultado_consulta_mensajes_linea_anterior",
            "{\"ciclosHaciaAtras\":1,\"idLineaConversacion\":100,\"idProcesamientoInternoMensaje\":200,\"cantidadEntradas\":2,\"estado\":\"cargada\"}",
            fecha);
        contexto.MetadataEntradasContextoIA.AddRange(
            CrearEntrada(linea.ID, null, null, null, 1, "user", "mensaje_entrada", "contexto anterior", fecha.AddMinutes(-5)),
            CrearEntrada(linea.ID, mensaje.ID, procesamiento.ID, null, 2, "user", "mensaje_entrada", mensaje.Contenido, fecha.AddMinutes(-4)),
            metadataEntradaDecision,
            metadataEntradaResultado,
            metadataEntradaDecisionConsulta,
            metadataEntradaResultadoConsulta,
            CrearEntrada(linea.ID, mensaje.ID, procesamiento.ID, metadataLimite.ID, 7, "assistant", "limite_ventana", "limite alcanzado", fecha.AddMinutes(1)));
        await contexto.SaveChangesAsync();

        DAOEjecucionComandoContexto intentoAnterior = new()
        {
            IDLineaConversacion = linea.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMetadataEntradaDecisionContextoIA = metadataEntradaDecision.ID,
            NumeroIntento = 1,
            ProveedorEjecucion = "lineacomando",
            IdentificadorExterno = "1001",
            CodigoComando = "pedido consultar",
            ParametrosJson = "{\"pedido\":\"54013\"}",
            IDEstadoEjecucionComandoContexto = "abandonada",
            Activa = false,
            FechaCreacion = fecha.AddMinutes(-3),
            FechaFinalizacion = fecha.AddMinutes(-2)
        };
        contexto.EjecucionesComandoContexto.Add(intentoAnterior);
        await contexto.SaveChangesAsync();

        contexto.EjecucionesComandoContexto.Add(new DAOEjecucionComandoContexto
        {
            IDEjecucionAnterior = intentoAnterior.ID,
            IDLineaConversacion = linea.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMetadataEntradaDecisionContextoIA = metadataEntradaDecision.ID,
            IDMetadataEntradaResultadoContextoIA = metadataEntradaResultado.ID,
            NumeroIntento = 2,
            ProveedorEjecucion = "lineacomando",
            IdentificadorExterno = "1002",
            CodigoComando = "pedido consultar",
            ParametrosJson = "{\"pedido\":\"54013\"}",
            IDEstadoEjecucionComandoContexto = "completada",
            Activa = false,
            FechaCreacion = fecha.AddMinutes(-2),
            FechaFinalizacion = fecha.AddMinutes(-1)
        });
        await contexto.SaveChangesAsync();

        return new DatosRenovacion(
            procesamiento.ID,
            mensaje.ID,
            linea.IDConversacion,
            linea.ID,
            metadataComando.ID);
    }

    private static async Task<DatosRenovacion> PrepararSegundoProcesamientoAsync(
        BaseDatosPrueba baseDatos,
        ResultadoRenovarLineaContexto renovacionAnterior)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DateTime fecha = DateTime.Now;
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = renovacionAnterior.IDLineaConversacion,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            Contenido = "segundo mensaje",
            IdentificadorExternoMensaje = $"segundo_{Guid.NewGuid():N}",
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
            IDEstadoProcesamientoInternoMensaje = "en_proceso",
            Intentos = 0,
            FechaCreacion = fecha
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        DAOInformacionTecnicaLlamadaIALineaConversacion metadataLimite = CrearInformacionTecnicaLlamadaIA(
            renovacionAnterior.IDLineaConversacion,
            procesamiento.ID,
            mensaje.ID,
            1,
            "LimiteVentanaAlcanzado");
        contexto.InformacionTecnicaLlamadasIALineaConversacion.Add(metadataLimite);
        await contexto.SaveChangesAsync();
        contexto.MetadataEntradasContextoIA.AddRange(
            CrearEntrada(renovacionAnterior.IDLineaConversacion, mensaje.ID, procesamiento.ID, null, 4, "user", "mensaje_entrada", mensaje.Contenido, fecha),
            CrearEntrada(renovacionAnterior.IDLineaConversacion, mensaje.ID, procesamiento.ID, metadataLimite.ID, 5, "assistant", "limite_ventana", "segundo limite", fecha));
        await contexto.SaveChangesAsync();

        return new DatosRenovacion(
            procesamiento.ID,
            mensaje.ID,
            renovacionAnterior.IDConversacion,
            renovacionAnterior.IDLineaConversacion,
            0);
    }

    private static async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearSegundoMensajeLoteAsync(
        BaseDatosPrueba baseDatos,
        long idLineaConversacion,
        DateTime fecha)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = idLineaConversacion,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            Contenido = "segundo mensaje del lote",
            IdentificadorExternoMensaje = $"lote_renovacion_{Guid.NewGuid():N}",
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
            IDEstadoProcesamientoInternoMensaje = "en_proceso",
            FechaCreacion = fecha
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        contexto.MetadataEntradasContextoIA.Add(
            CrearEntrada(
                idLineaConversacion,
                mensaje.ID,
                procesamiento.ID,
                null,
                8,
                "user",
                "mensaje_entrada",
                mensaje.Contenido,
                fecha));
        await contexto.SaveChangesAsync();

        return (mensaje, procesamiento);
    }

    private static DAOInformacionTecnicaLlamadaIALineaConversacion CrearInformacionTecnicaLlamadaIA(
        long idLinea,
        long idProcesamiento,
        long idMensaje,
        int iteracion,
        string accion)
    {
        return new DAOInformacionTecnicaLlamadaIALineaConversacion
        {
            IDLineaConversacion = idLinea,
            IDProcesamientoInternoMensaje = idProcesamiento,
            IDMensaje = idMensaje,
            Proveedor = "fake",
            Modelo = "fake",
            Adaptador = "fake",
            Iteracion = iteracion,
            AccionDecidida = accion,
            FechaCreacion = DateTime.Now
        };
    }

    private static DAOMetadataEntradaContextoIA CrearEntrada(
        long idLinea,
        long? idMensaje,
        long? idProcesamiento,
        long? idMetadata,
        int orden,
        string rol,
        string tipo,
        string? contenido,
        DateTime fecha)
    {
        return new DAOMetadataEntradaContextoIA
        {
            IDLineaConversacion = idLinea,
            IDMensaje = idMensaje,
            IDProcesamientoInternoMensaje = idProcesamiento,
            IDInformacionTecnicaLlamadaIA = idMetadata,
            Orden = orden,
            IDRolContextoIA = rol,
            IDTipoEntradaContextoIA = tipo,
            Contenido = contenido,
            FechaEntrada = fecha,
            FechaCreacion = fecha
        };
    }

    private static SolicitudRenovarLineaContexto CrearSolicitud(
        DatosRenovacion datos,
        string contenido)
    {
        return new SolicitudRenovarLineaContexto
        {
            IDProcesamientoInternoMensaje = datos.IDProcesamiento,
            IDMensaje = datos.IDMensaje,
            IDConversacion = datos.IDConversacion,
            IDLineaConversacionOrigen = datos.IDLineaOrigen,
            Compactacion = ResultadoCompactacionIntencionContexto.Exito(
                contenido,
                [
                    CrearInformacionTecnicaCompactacion("compactacion fragmento"),
                    CrearInformacionTecnicaCompactacion("compactacion final")
                ])
        };
    }

    private static InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaCompactacion(string contenido)
    {
        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = "fake",
            Modelo = "fake",
            Adaptador = "fake",
            Iteracion = 3,
            AccionDecidida = "Compactar",
            Content = contenido
        };
    }

    private sealed record DatosRenovacion(
        long IDProcesamiento,
        long IDMensaje,
        long IDConversacion,
        long IDLineaOrigen,
        long IDMetadataComando);
}
