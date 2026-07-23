using System.Text.Json;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

namespace AplicacionTest;

public class ContextoConversacionServicioTest
{
    [Fact]
    public async Task ResolverAsync_FiltrosConfigurados_DebeEjecutarlosEnOrden()
    {
        List<string> orden = [];
        FiltroContextoFake filtroA = new("A", orden);
        FiltroContextoFake filtroB = new("B", orden);
        IntencionContextoFake intencion = new(NoResponder());
        ContextoConversacionServicio servicio = CrearServicio([filtroA, filtroB], intencion);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.SinSalidas, resultado.TipoResultado);
        Assert.Equal(["A", "B"], orden);
    }

    [Fact]
    public async Task ResolverAsync_FiltroRetornaError_DebeCortarFlujoSinEjecutarIA()
    {
        List<string> orden = [];
        FiltroContextoFake filtroA = new("A", orden);
        FiltroContextoFake filtroB = new("B", orden, "Filtro B no pasa.");
        IntencionContextoFake intencion = new(NoResponder());
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [filtroA, filtroB],
            intencion,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("Filtro B no pasa", resultado.Error);
        Assert.Equal(["A", "B"], orden);
        Assert.Empty(intencion.Llamadas);
        Assert.Empty(registrar.InformacionesTecnicasLlamadasIA);
        AssertEntradas(registrar, ("user", "mensaje_entrada"));
    }

    [Fact]
    public async Task ResolverAsync_IADebeRecibirCatalogoComandos()
    {
        ComandoContexto comando = CrearComando("consultar_pedido");
        IntencionContextoFake intencion = new(NoResponder());
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [comando]);

        await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        SolicitudIntencionContexto solicitudIA = Assert.Single(intencion.Llamadas);
        ComandoContexto comandoRecibido = Assert.Single(solicitudIA.Comandos);
        Assert.Equal("consultar_pedido", comandoRecibido.Codigo);
        Assert.Equal("Consulta pedido", comandoRecibido.Descripcion);
        Assert.Equal("conversacion", comandoRecibido.Alcance);
        Assert.Equal("usar solo con numero de pedido", comandoRecibido.ReglasUso);
        Assert.True(comandoRecibido.Parametros.ContainsKey("numero_pedido"));
    }

    [Fact]
    public async Task ResolverAsync_ResultadoResponder_DebeRetornarConSalidas()
    {
        MensajeSalienteContexto mensaje = CrearMensajeSaliente();
        IntencionContextoFake intencion = new(Responder(mensaje));
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Single(resultado.MensajesSalientes);
        InformacionTecnicaLlamadaIAContexto metadata = Assert.Single(registrar.InformacionesTecnicasLlamadasIA);
        Assert.Equal(1, metadata.Iteracion);
        Assert.Equal(nameof(AccionContextoTipo.Responder), metadata.AccionDecidida);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "respuesta_final"));
    }

    [Fact]
    public async Task ResolverAsync_ResultadoNoResponder_DebeRetornarSinSalidas()
    {
        IntencionContextoFake intencion = new(NoResponder());
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.SinSalidas, resultado.TipoResultado);
        Assert.Empty(resultado.MensajesSalientes);
        Assert.Single(registrar.InformacionesTecnicasLlamadasIA);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "no_responder"));
    }

    [Fact]
    public async Task ResolverAsync_ComandoExitoso_DebeReingresarResultadoAFiltrosEIA()
    {
        List<string> orden = [];
        FiltroContextoFake filtro = new("filtro", orden);
        IntencionContextoFake intencion = new(
            PedirComando("consultar_pedido", toolCallID: "call-comando-1"),
            Responder(CrearMensajeSaliente()));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Exitoso("pedido encontrado");
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [filtro],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(1, ejecutor.Llamadas);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, filtro.Llamadas);
        Assert.Contains(intencion.Llamadas[1].DatosIntermedios, dato => dato.Tipo == "comando" && dato.Contenido == "pedido encontrado");
        Assert.Equal(2, registrar.InformacionesTecnicasLlamadasIA.Count);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_comando"),
            ("tool", "resultado_comando"),
            ("assistant", "respuesta_final"));
        MetadataEntradaContextoIA decisionComando = Assert.Single(
            registrar.Entradas,
            entrada => entrada.IDTipoEntradaContextoIA == "decision_comando");
        MetadataEntradaContextoIA resultadoComando = Assert.Single(
            registrar.Entradas,
            entrada => entrada.IDTipoEntradaContextoIA == "resultado_comando");
        Assert.Equal("call-comando-1", decisionComando.ToolCallID);
        Assert.Equal(decisionComando.ToolCallID, resultadoComando.ToolCallID);
    }

    [Fact]
    public async Task ResolverAsync_ComandoInvalidoLimitantes_DebeRetornarErrorSinInvocarCola()
    {
        IntencionContextoFake intencion = new(PedirComando("comando_no_autorizado"));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Exitoso("no debe ejecutarse");
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("Comando no autorizado", resultado.Error);
        Assert.Equal(0, ejecutor.Llamadas);
        Assert.Single(registrar.InformacionesTecnicasLlamadasIA);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_comando"));
    }

    [Fact]
    public async Task ResolverAsync_ColaComandoFalla_DebeRetornarErrorControlado()
    {
        IntencionContextoFake intencion = new(PedirComando("consultar_pedido"));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Fallido("cola no disponible");
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("cola no disponible", resultado.Error);
        Assert.Equal(1, ejecutor.Llamadas);
        Assert.Single(intencion.Llamadas);
        Assert.Single(registrar.InformacionesTecnicasLlamadasIA);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_comando"),
            ("tool", "resultado_comando"));
    }

    [Fact]
    public async Task ResolverAsync_ConsultaMensajesAnteriores_DebeIncorporarCicloDespuesDelResultadoTool()
    {
        FiltroContextoFake filtro = new("filtro", []);
        IntencionContextoFake intencion = new(
            ConsultarMensajesLineaAnterior(1, "call-consulta-1"),
            Responder(CrearMensajeSaliente()));
        ConsultaMensajesLineaAnteriorFake consulta = ConsultaMensajesLineaAnteriorFake.ConCiclo(
            new MetadataEntradaContextoIA
            {
                ID = 90,
                IDLineaConversacion = 3,
                IDMensaje = 80,
                IDProcesamientoInternoMensaje = 70,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = "mensaje anterior",
                FechaEntrada = DateTime.Now.AddDays(-1)
            },
            new MetadataEntradaContextoIA
            {
                ID = 91,
                IDLineaConversacion = 3,
                IDMensaje = 80,
                IDProcesamientoInternoMensaje = 70,
                Orden = 2,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "respuesta anterior",
                FechaEntrada = DateTime.Now.AddDays(-1).AddMinutes(1)
            });
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [filtro],
            intencion,
            consultaMensajesAnteriores: consulta,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(1, consulta.ConsultasPorPosicion);
        Assert.Equal(1, consulta.ConsultasPorReferencia);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, filtro.Llamadas);
        Assert.Empty(intencion.Llamadas[1].DatosIntermedios);
        Assert.Equal(
            ["mensaje_entrada", "decision_consulta_mensajes_linea_anterior", "resultado_consulta_mensajes_linea_anterior", "mensaje_entrada", "respuesta_final"],
            intencion.Llamadas[1].MetadataEntradasContextoIA.Select(entrada => entrada.IDTipoEntradaContextoIA));
        Assert.Equal(2, registrar.InformacionesTecnicasLlamadasIA.Count);
        Assert.Empty(intencion.Compactaciones);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_consulta_mensajes_linea_anterior"),
            ("tool", "resultado_consulta_mensajes_linea_anterior"),
            ("assistant", "respuesta_final"));
        MetadataEntradaContextoIA decisionConsulta = Assert.Single(
            registrar.Entradas,
            entrada => entrada.IDTipoEntradaContextoIA == "decision_consulta_mensajes_linea_anterior");
        MetadataEntradaContextoIA resultadoConsulta = Assert.Single(
            registrar.Entradas,
            entrada => entrada.IDTipoEntradaContextoIA == "resultado_consulta_mensajes_linea_anterior");
        Assert.Equal("call-consulta-1", decisionConsulta.ToolCallID);
        Assert.Equal(decisionConsulta.ToolCallID, resultadoConsulta.ToolCallID);
        Assert.DoesNotContain(registrar.Entradas, entrada => entrada.IDLineaConversacion == 3);
    }

    [Fact]
    public async Task ResolverAsync_DosConsultasAnteriores_DebeConservarCadaCicloEnSuPosicionSinPersistirCopias()
    {
        MetadataEntradaContextoIA entradaCicloReciente = new()
        {
            ID = 90,
            IDLineaConversacion = 30,
            IDMensaje = 80,
            IDProcesamientoInternoMensaje = 70,
            Orden = 1,
            IDRolContextoIA = "user",
            IDTipoEntradaContextoIA = "mensaje_entrada",
            Contenido = "ciclo anterior reciente",
            FechaEntrada = DateTime.Now.AddDays(-1)
        };
        MetadataEntradaContextoIA entradaCicloAntiguo = new()
        {
            ID = 91,
            IDLineaConversacion = 20,
            IDMensaje = 60,
            IDProcesamientoInternoMensaje = 50,
            Orden = 1,
            IDRolContextoIA = "user",
            IDTipoEntradaContextoIA = "mensaje_entrada",
            Contenido = "ciclo anterior antiguo",
            FechaEntrada = DateTime.Now.AddDays(-2)
        };
        ConsultaMensajesLineaAnteriorFake consulta = ConsultaMensajesLineaAnteriorFake.ConCiclos(
            [entradaCicloReciente],
            [entradaCicloAntiguo]);
        IntencionContextoFake intencion = new(
            ConsultarMensajesLineaAnterior(1, "call-consulta-1"),
            ConsultarMensajesLineaAnterior(2, "call-consulta-2"),
            Responder(CrearMensajeSaliente()));
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("filtro", [])],
            intencion,
            consultaMensajesAnteriores: consulta,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(3, intencion.Llamadas.Count);
        Assert.Equal(
            [
                "mensaje_entrada",
                "decision_consulta_mensajes_linea_anterior",
                "resultado_consulta_mensajes_linea_anterior",
                "mensaje_entrada",
                "decision_consulta_mensajes_linea_anterior",
                "resultado_consulta_mensajes_linea_anterior",
                "mensaje_entrada"
            ],
            intencion.Llamadas[2].MetadataEntradasContextoIA.Select(entrada => entrada.IDTipoEntradaContextoIA));
        Assert.Equal(
            [
                "Necesito consultar un pedido",
                JsonSerializer.Serialize(new { accion = "consultar_mensajes_linea_anterior", ciclosHaciaAtras = 1 }),
                Assert.IsType<string>(registrar.Entradas.Single(entrada => entrada.ToolCallID == "call-consulta-1" && entrada.IDRolContextoIA == "tool").Contenido),
                "ciclo anterior reciente",
                JsonSerializer.Serialize(new { accion = "consultar_mensajes_linea_anterior", ciclosHaciaAtras = 2 }),
                Assert.IsType<string>(registrar.Entradas.Single(entrada => entrada.ToolCallID == "call-consulta-2" && entrada.IDRolContextoIA == "tool").Contenido),
                "ciclo anterior antiguo"
            ],
            intencion.Llamadas[2].MetadataEntradasContextoIA.Select(entrada => entrada.Contenido));
        Assert.Equal(2, consulta.ConsultasPorPosicion);
        Assert.Equal(3, consulta.ConsultasPorReferencia);
        Assert.DoesNotContain(registrar.Entradas, entrada => entrada.ID is 90 or 91);
        Assert.DoesNotContain(registrar.Entradas, entrada => entrada.IDLineaConversacion is 20 or 30);
    }

    [Fact]
    public async Task ResolverAsync_LimiteTrasConsultaAnterior_DebeCompactarCicloPrestadoYConservarConsultaActual()
    {
        MetadataEntradaContextoIA entradaAnterior = new()
        {
            ID = 90,
            IDLineaConversacion = 30,
            IDMensaje = 80,
            IDProcesamientoInternoMensaje = 70,
            Orden = 1,
            IDRolContextoIA = "user",
            IDTipoEntradaContextoIA = "mensaje_entrada",
            Contenido = "dato anterior que causa el limite",
            FechaEntrada = DateTime.Now.AddDays(-1)
        };
        ConsultaMensajesLineaAnteriorFake consulta = ConsultaMensajesLineaAnteriorFake.ConCiclo(entradaAnterior);
        ResultadoCompactacionIntencionContexto compactacion = ResultadoCompactacionIntencionContexto.Exito(
            "snapshot con ciclo anterior",
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado));
        IntencionContextoFake intencion = new(
            compactacion,
            ConsultarMensajesLineaAnterior(1, "call-consulta-1"),
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado),
                "limite despues de incorporar ciclo anterior",
                DeteccionLimiteVentanaContextoTipo.RechazoProveedor));
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("filtro", [])],
            intencion,
            consultaMensajesAnteriores: consulta,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado, resultado.TipoResultado);
        SolicitudCompactacionIntencionContexto solicitudCompactacion = Assert.Single(intencion.Compactaciones);
        MetadataEntradaContextoIA cicloCompactado = Assert.Single(solicitudCompactacion.MetadataEntradasContextoIA);
        Assert.Same(entradaAnterior, cicloCompactado);
        Assert.DoesNotContain(
            solicitudCompactacion.MetadataEntradasContextoIA,
            entrada => entrada.IDProcesamientoInternoMensaje == 1);
        Assert.Contains(
            registrar.Entradas,
            entrada => entrada.IDTipoEntradaContextoIA == "resultado_consulta_mensajes_linea_anterior");
        Assert.DoesNotContain(registrar.Entradas, entrada => entrada.ID == entradaAnterior.ID);
    }

    [Fact]
    public async Task ResolverAsync_ReferenciaConsultaCargadaCorrupta_DebeFallarAntesDeInvocarIA()
    {
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 77,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                Orden = 1,
                IDRolContextoIA = "tool",
                IDTipoEntradaContextoIA = "resultado_consulta_mensajes_linea_anterior",
                Contenido = "{\"estado\":\"cargada\"}",
                FechaEntrada = DateTime.Now.AddMinutes(-1)
            });
        IntencionContextoFake intencion = new(NoResponder());
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("filtro", [])],
            intencion,
            registrarContextoIA: registrar);

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None));

        Assert.Contains("no identifica el ciclo anterior", excepcion.Message);
        Assert.Empty(intencion.Llamadas);
    }

    [Fact]
    public async Task ResolverAsync_ConsultaYaIncorporadaEnCompactacion_NoDebeExpandirOtraVezElCiclo()
    {
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 77,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                IDCompactacionContextoIncorporada = 55,
                Orden = 1,
                IDRolContextoIA = "tool",
                IDTipoEntradaContextoIA = "resultado_consulta_mensajes_linea_anterior",
                Contenido = "{\"idLineaConversacion\":30,\"idProcesamientoInternoMensaje\":70,\"estado\":\"cargada\"}",
                FechaEntrada = DateTime.Now.AddMinutes(-1)
            });
        ConsultaMensajesLineaAnteriorFake consulta = ConsultaMensajesLineaAnteriorFake.ConCiclo(
            new MetadataEntradaContextoIA
            {
                ID = 90,
                IDLineaConversacion = 30,
                IDProcesamientoInternoMensaje = 70,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = "no debe volver a expandirse",
                FechaEntrada = DateTime.Now.AddDays(-1)
            });
        IntencionContextoFake intencion = new(NoResponder());
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("filtro", [])],
            intencion,
            consultaMensajesAnteriores: consulta,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.SinSalidas, resultado.TipoResultado);
        SolicitudIntencionContexto solicitudIA = Assert.Single(intencion.Llamadas);
        Assert.DoesNotContain(
            solicitudIA.MetadataEntradasContextoIA,
            entrada => entrada.Contenido == "no debe volver a expandirse");
        Assert.Equal(0, consulta.ConsultasPorReferencia);
    }

    [Fact]
    public async Task ResolverAsync_LimiteVentana_DebeCompactarContextoAnteriorYRetornarRenovacion()
    {
        CompactacionContextoConversacion compactacionInicial = new()
        {
            ID = 71,
            IDConversacion = 3,
            IDLineaConversacionOrigen = 70,
            Version = 1,
            Contenido = "snapshot anterior",
            FechaCreacion = DateTime.Now.AddHours(-1)
        };
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 80,
                IDLineaConversacion = 4,
                IDMensaje = 79,
                IDProcesamientoInternoMensaje = 78,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = "mensaje anterior de la linea",
                FechaEntrada = DateTime.Now.AddMinutes(-10)
            });
        ResultadoCompactacionIntencionContexto compactacion = ResultadoCompactacionIntencionContexto.Exito(
            "snapshot acumulado",
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado));
        IntencionContextoFake intencion = new(
            compactacion,
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado),
                "limite alcanzado",
                DeteccionLimiteVentanaContextoTipo.Estimado));
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar,
            compactacionContextoInicial: compactacionInicial);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado, resultado.TipoResultado);
        Assert.Same(compactacion, resultado.Compactacion);
        SolicitudCompactacionIntencionContexto solicitudCompactacion = Assert.Single(intencion.Compactaciones);
        Assert.Same(compactacionInicial, solicitudCompactacion.CompactacionContextoInicial);
        MetadataEntradaContextoIA entradaCompactada = Assert.Single(solicitudCompactacion.MetadataEntradasContextoIA);
        Assert.Equal(78, entradaCompactada.IDProcesamientoInternoMensaje);
        Assert.DoesNotContain(
            solicitudCompactacion.MetadataEntradasContextoIA,
            entrada => entrada.IDProcesamientoInternoMensaje == 1);
        SolicitudIntencionContexto solicitudDecision = Assert.Single(intencion.Llamadas);
        Assert.Same(compactacionInicial, solicitudDecision.CompactacionContextoInicial);
        Assert.Equal("Compactar", compactacion.InformacionTecnicaLlamadaIA.AccionDecidida);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("user", "mensaje_entrada"),
            ("assistant", "limite_ventana"));
    }

    [Fact]
    public async Task ResolverAsync_LimiteSinContextoAnterior_DebeFallarSinCompactar()
    {
        IntencionContextoFake intencion = new(
            ResultadoCompactacionIntencionContexto.Exito(
                "no debe usarse",
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado)),
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado),
                "limite alcanzado",
                DeteccionLimiteVentanaContextoTipo.Estimado));
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("contexto anterior reducible", resultado.Error);
        Assert.Empty(intencion.Compactaciones);
    }

    [Fact]
    public async Task ResolverAsync_CompactacionFallida_DebeTerminarEnError()
    {
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 90,
                IDLineaConversacion = 4,
                IDMensaje = 89,
                IDProcesamientoInternoMensaje = 88,
                Orden = 1,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "contexto anterior",
                FechaEntrada = DateTime.Now.AddMinutes(-5)
            });
        IntencionContextoFake intencion = new(
            ResultadoCompactacionIntencionContexto.Fallo(
                "fallo al resumir",
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado)),
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado),
                "limite alcanzado",
                DeteccionLimiteVentanaContextoTipo.RechazoProveedor));
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("fallo al resumir", resultado.Error);
        Assert.Single(intencion.Compactaciones);
        Assert.Null(resultado.Compactacion);
    }

    [Fact]
    public async Task ResolverAsync_InformacionTecnicaCompactacionIncompleta_DebeImpedirRenovacion()
    {
        InformacionTecnicaLlamadaIAContexto informacionTecnicaCompactacion = CrearInformacionTecnicaLlamadaIA(
            AccionContextoTipo.LimiteVentanaAlcanzado);
        ResultadoCompactacionIntencionContexto compactacion = ResultadoCompactacionIntencionContexto.Exito(
            "snapshot",
            informacionTecnicaCompactacion);
        informacionTecnicaCompactacion.Adaptador = string.Empty;
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 100,
                IDLineaConversacion = 4,
                IDProcesamientoInternoMensaje = 99,
                Orden = 1,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "contexto anterior",
                FechaEntrada = DateTime.Now.AddMinutes(-1)
            });
        IntencionContextoFake intencion = new(
            compactacion,
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.LimiteVentanaAlcanzado),
                "limite alcanzado",
                DeteccionLimiteVentanaContextoTipo.RechazoProveedor));
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None));

        Assert.Contains("adaptador", excepcion.Message);
        Assert.Single(intencion.Compactaciones);
    }

    [Fact]
    public async Task ResolverAsync_MaximoIteraciones_DebeCortarCicloInfinito()
    {
        IntencionContextoFake intencion = new(
            ConsultarMensajesLineaAnterior(1),
            ConsultarMensajesLineaAnterior(1),
            ConsultarMensajesLineaAnterior(1));
        ConsultaMensajesLineaAnteriorFake consulta = ConsultaMensajesLineaAnteriorFake.ConCiclo(
            new MetadataEntradaContextoIA
            {
                ID = 501,
                IDLineaConversacion = 3,
                IDProcesamientoInternoMensaje = 500,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = "anterior",
                FechaEntrada = DateTime.Now.AddDays(-1)
            });
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            consultaMensajesAnteriores: consulta,
            registrarContextoIA: registrar,
            maximoIteraciones: 2);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("maximo de iteraciones", resultado.Error);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, consulta.ConsultasPorPosicion);
        Assert.Equal(2, registrar.InformacionesTecnicasLlamadasIA.Count);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_consulta_mensajes_linea_anterior"),
            ("tool", "resultado_consulta_mensajes_linea_anterior"),
            ("assistant", "decision_consulta_mensajes_linea_anterior"),
            ("tool", "resultado_consulta_mensajes_linea_anterior"));
    }

    [Fact]
    public async Task ResolverAsync_DebeEnviarALaIALasEntradasDeTodaLaLineaConInformacionTecnica()
    {
        InformacionTecnicaLlamadaIAContexto metadataAnterior = CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.Responder);
        metadataAnterior.Iteracion = 1;
        metadataAnterior.Reasoning = "razonamiento anterior";
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 10,
                IDLineaConversacion = 4,
                IDMensaje = 20,
                IDProcesamientoInternoMensaje = 30,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = "mensaje anterior",
                FechaEntrada = DateTime.Now.AddMinutes(-5)
            },
            new MetadataEntradaContextoIA
            {
                ID = 11,
                IDLineaConversacion = 4,
                IDMensaje = 20,
                IDProcesamientoInternoMensaje = 30,
                IDInformacionTecnicaLlamadaIA = 40,
                Orden = 2,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "respuesta anterior",
                FechaEntrada = DateTime.Now.AddMinutes(-4),
                InformacionTecnicaLlamadaIA = metadataAnterior
            });
        IntencionContextoFake intencion = new(NoResponder());
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        SolicitudIntencionContexto solicitudIA = Assert.Single(intencion.Llamadas);
        Assert.Equal(3, solicitudIA.MetadataEntradasContextoIA.Count);
        Assert.Equal([1, 2, 3], solicitudIA.MetadataEntradasContextoIA.Select(entrada => entrada.Orden));
        MetadataEntradaContextoIA entradaAnterior = solicitudIA.MetadataEntradasContextoIA[1];
        Assert.NotNull(entradaAnterior.InformacionTecnicaLlamadaIA);
        Assert.Equal("razonamiento anterior", entradaAnterior.InformacionTecnicaLlamadaIA.Reasoning);
        Assert.Equal(2, solicitudIA.MetadataEntradasContextoIA.Count(
            entrada => entrada.IDProcesamientoInternoMensaje != 1));
    }

    [Fact]
    public async Task ResolverAsync_ReinicioConResultadoComandoPersistido_DebeContinuarSinReejecutarComando()
    {
        InformacionTecnicaLlamadaIAContexto metadataComando = CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.Comando);
        metadataComando.Iteracion = 1;
        RegistrarContextoIAAplicacionFake registrar = new(
            new MetadataEntradaContextoIA
            {
                ID = 10,
                IDLineaConversacion = 4,
                IDMensaje = 99,
                IDProcesamientoInternoMensaje = 88,
                Orden = 1,
                IDRolContextoIA = "tool",
                IDTipoEntradaContextoIA = "resultado_comando",
                Contenido = "resultado de otro procesamiento",
                FechaEntrada = DateTime.Now.AddMinutes(-3)
            },
            new MetadataEntradaContextoIA
            {
                ID = 11,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                Orden = 2,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = "Necesito consultar un pedido",
                FechaEntrada = DateTime.Now.AddMinutes(-2)
            },
            new MetadataEntradaContextoIA
            {
                ID = 12,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                IDInformacionTecnicaLlamadaIA = 50,
                Orden = 3,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "decision_comando",
                Contenido = "comando:consultar_pedido",
                FechaEntrada = DateTime.Now.AddMinutes(-1),
                InformacionTecnicaLlamadaIA = metadataComando
            },
            new MetadataEntradaContextoIA
            {
                ID = 13,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                Orden = 4,
                IDRolContextoIA = "tool",
                IDTipoEntradaContextoIA = "resultado_comando",
                Contenido = "pedido recuperado",
                FechaEntrada = DateTime.Now
            });
        IntencionContextoFake intencion = new(Responder(CrearMensajeSaliente()));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Exitoso("no debe ejecutarse");
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(0, ejecutor.Llamadas);
        SolicitudIntencionContexto solicitudIA = Assert.Single(intencion.Llamadas);
        Assert.Equal(2, solicitudIA.Iteracion);
        DatoIntermedioContexto dato = Assert.Single(solicitudIA.DatosIntermedios);
        Assert.Equal("comando", dato.Tipo);
        Assert.Equal("pedido recuperado", dato.Contenido);
        Assert.DoesNotContain(
            solicitudIA.DatosIntermedios,
            datoActual => datoActual.Contenido == "resultado de otro procesamiento");
    }

    [Fact]
    public async Task ResolverAsync_EjecucionActiva_DebeResolverlaAntesDeInvocarIA()
    {
        TaskCompletionSource<ResultadoEjecucionComandoContexto?> recuperacion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IntencionContextoFake intencion = new(NoResponder());
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            registrarContextoIA: registrar,
            recuperacionEjecucion: recuperacion.Task);

        Task<ResultadoContextoConversacion> tarea = servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);
        await Task.Yield();

        Assert.Empty(intencion.Llamadas);

        recuperacion.SetResult(new ResultadoEjecucionComandoContexto
        {
            Resultado = ResultadoComandoContexto.Exito("resultado recuperado"),
            MetadataEntradaResultado = new MetadataEntradaContextoIA
            {
                ID = 90,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                Orden = 2,
                IDRolContextoIA = "tool",
                IDTipoEntradaContextoIA = "resultado_comando",
                Contenido = "resultado recuperado",
                FechaEntrada = DateTime.Now
            }
        });

        ResultadoContextoConversacion resultado = await tarea;

        Assert.Equal(ResultadoContextoConversacionTipo.SinSalidas, resultado.TipoResultado);
        SolicitudIntencionContexto solicitudIA = Assert.Single(intencion.Llamadas);
        Assert.Contains(
            solicitudIA.DatosIntermedios,
            dato => dato.Tipo == "comando" && dato.Contenido == "resultado recuperado");
    }

    [Theory]
    [InlineData("proveedor")]
    [InlineData("modelo")]
    [InlineData("adaptador")]
    public async Task ResolverAsync_InformacionTecnicaIncompleta_DebeFallarAntesDePersistir(string campo)
    {
        InformacionTecnicaLlamadaIAContexto metadata = CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.NoResponder);
        ResultadoIntencionContexto decision = ResultadoIntencionContexto.NoResponder(metadata, "no_responder");

        if (campo == "proveedor")
        {
            metadata.Proveedor = string.Empty;
        }
        else if (campo == "modelo")
        {
            metadata.Modelo = string.Empty;
        }
        else
        {
            metadata.Adaptador = string.Empty;
        }

        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            new IntencionContextoFake(decision),
            registrarContextoIA: registrar);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None));

        Assert.Empty(registrar.InformacionesTecnicasLlamadasIA);
        AssertEntradas(registrar, ("user", "mensaje_entrada"));
    }

    [Fact]
    public void ResultadoIntencionContexto_DebeExigirInformacionTecnicaYContenido()
    {
        InformacionTecnicaLlamadaIAContexto metadataSinProveedor = CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.NoResponder);
        metadataSinProveedor.Proveedor = string.Empty;
        InformacionTecnicaLlamadaIAContexto metadataSinModelo = CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.NoResponder);
        metadataSinModelo.Modelo = string.Empty;
        InformacionTecnicaLlamadaIAContexto metadataSinAdaptador = CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.NoResponder);
        metadataSinAdaptador.Adaptador = string.Empty;

        Assert.Throws<ArgumentNullException>(
            () => ResultadoIntencionContexto.NoResponder(null!, "contenido"));
        Assert.Throws<ArgumentException>(
            () => ResultadoIntencionContexto.NoResponder(
                CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.NoResponder),
                string.Empty));
        Assert.Throws<ArgumentException>(
            () => ResultadoIntencionContexto.NoResponder(metadataSinProveedor, "contenido"));
        Assert.Throws<ArgumentException>(
            () => ResultadoIntencionContexto.NoResponder(metadataSinModelo, "contenido"));
        Assert.Throws<ArgumentException>(
            () => ResultadoIntencionContexto.NoResponder(metadataSinAdaptador, "contenido"));
    }

    private static void AssertEntradas(
        RegistrarContextoIAAplicacionFake registrar,
        params (string Rol, string Tipo)[] esperadas)
    {
        (string Rol, string Tipo)[] actuales = registrar.Entradas
            .OrderBy(entrada => entrada.Orden)
            .Select(entrada => (entrada.IDRolContextoIA, entrada.IDTipoEntradaContextoIA))
            .ToArray();

        Assert.Equal(esperadas, actuales);
        Assert.Equal(
            Enumerable.Range(1, esperadas.Length),
            registrar.Entradas.OrderBy(entrada => entrada.Orden).Select(entrada => entrada.Orden));
    }

    private static ContextoConversacionServicio CrearServicio(
        IReadOnlyList<IFiltroContextoConversacion> filtros,
        IntencionContextoFake intencion,
        IReadOnlyList<ComandoContexto>? catalogo = null,
        EjecutorComandoContextoFake? ejecutor = null,
        ConsultaMensajesLineaAnteriorFake? consultaMensajesAnteriores = null,
        RegistrarContextoIAAplicacionFake? registrarContextoIA = null,
        CompactacionContextoConversacion? compactacionContextoInicial = null,
        Task<ResultadoEjecucionComandoContexto?>? recuperacionEjecucion = null,
        int maximoIteraciones = 5)
    {
        RegistrarContextoIAAplicacionFake registrar = registrarContextoIA ?? new RegistrarContextoIAAplicacionFake();
        EjecutorComandoContextoFake ejecutorFinal = ejecutor ?? EjecutorComandoContextoFake.Exitoso("resultado");
        return new ContextoConversacionServicio(
            filtros,
            intencion,
            new ProveedorCatalogoComandoContextoFake(catalogo ?? []),
            new EjecucionComandoContextoAplicacionFake(ejecutorFinal, registrar, recuperacionEjecucion),
            consultaMensajesAnteriores ?? ConsultaMensajesLineaAnteriorFake.SinResultados(),
            registrar,
            new CompactacionContextoConversacionAplicacionFake(compactacionContextoInicial),
            new ConfiguracionContextoConversacion
            {
                MaximoIteraciones = maximoIteraciones
            });
    }

    private static SolicitudContextoConversacion CrearSolicitud()
    {
        return new SolicitudContextoConversacion
        {
            IDProcesamientoInternoMensaje = 1,
            IDMensaje = 2,
            IDConversacion = 3,
            IDLineaConversacion = 4,
            IDCuentaCanal = 5,
            TipoMensaje = "texto",
            TelefonoOrigen = "3000000001",
            TelefonoDestino = "3000000002",
            Contenido = "Necesito consultar un pedido",
            FechaMensaje = DateTime.Now
        };
    }

    private static MensajeSalienteContexto CrearMensajeSaliente()
    {
        return new MensajeSalienteContexto
        {
            TipoMensaje = "texto",
            Contenido = "Respuesta final",
            FechaMensaje = DateTime.Now
        };
    }

    private static ComandoContexto CrearComando(string codigo)
    {
        return new ComandoContexto
        {
            Codigo = codigo,
            Descripcion = "Consulta pedido",
            Alcance = "conversacion",
            ReglasUso = "usar solo con numero de pedido",
            Parametros = new Dictionary<string, string>
            {
                ["numero_pedido"] = "string"
            }
        };
    }

    private static ResultadoIntencionContexto NoResponder()
    {
        return ResultadoIntencionContexto.NoResponder(
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.NoResponder),
            "no_responder");
    }

    private static ResultadoIntencionContexto Responder(MensajeSalienteContexto mensaje)
    {
        return ResultadoIntencionContexto.Responder(
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.Responder),
            mensaje.Contenido ?? "respuesta",
            mensaje);
    }

    private static ResultadoIntencionContexto PedirComando(
        string codigoComando,
        Dictionary<string, string>? parametros = null,
        string? toolCallID = null)
    {
        return ResultadoIntencionContexto.PedirComando(
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.Comando),
            $"comando:{codigoComando}",
            codigoComando,
            parametros,
            toolCallID);
    }

    private static ResultadoIntencionContexto ConsultarMensajesLineaAnterior(
        int ciclosHaciaAtras,
        string? toolCallID = null)
    {
        return ResultadoIntencionContexto.ConsultarMensajesLineaAnterior(
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.ConsultarMensajesLineaAnterior),
            JsonSerializer.Serialize(new { accion = "consultar_mensajes_linea_anterior", ciclosHaciaAtras }),
            ciclosHaciaAtras,
            toolCallID);
    }

    private static ResultadoIntencionContexto ConError(string error)
    {
        return ResultadoIntencionContexto.ConError(
            CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.Error),
            error,
            error);
    }

    private static InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaLlamadaIA(AccionContextoTipo accion)
    {
        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = "fake",
            Modelo = "fake",
            Adaptador = "fake",
            AccionDecidida = accion.ToString(),
            Content = accion.ToString()
        };
    }

    private sealed class FiltroContextoFake : IFiltroContextoConversacion
    {
        private readonly string nombre;
        private readonly List<string> orden;
        private readonly string? error;

        public FiltroContextoFake(string nombre, List<string> orden, string? error = null)
        {
            this.nombre = nombre;
            this.orden = orden;
            this.error = error;
        }

        public int Llamadas { get; private set; }

        public Task<ResultadoFiltroContexto> EjecutarAsync(
            EstadoIteracionContextoConversacion estado,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            orden.Add(nombre);

            if (error is not null)
            {
                return Task.FromResult(ResultadoFiltroContexto.DetenerConError(error));
            }

            return Task.FromResult(ResultadoFiltroContexto.ContinuarFlujo());
        }
    }

    private sealed class IntencionContextoFake : IIntencionContextoConversacionServicio
    {
        private readonly Queue<ResultadoIntencionContexto> resultados;
        private readonly ResultadoCompactacionIntencionContexto? resultadoCompactacion;

        public IntencionContextoFake(params ResultadoIntencionContexto[] resultados)
        {
            this.resultados = new Queue<ResultadoIntencionContexto>(resultados);
        }

        public IntencionContextoFake(
            ResultadoCompactacionIntencionContexto resultadoCompactacion,
            params ResultadoIntencionContexto[] resultados)
        {
            this.resultadoCompactacion = resultadoCompactacion;
            this.resultados = new Queue<ResultadoIntencionContexto>(resultados);
        }

        public List<SolicitudIntencionContexto> Llamadas { get; } = [];
        public List<SolicitudCompactacionIntencionContexto> Compactaciones { get; } = [];

        public Task<ResultadoIntencionContexto> DecidirAsync(
            SolicitudIntencionContexto solicitud,
            CancellationToken cancellationToken)
        {
            Llamadas.Add(new SolicitudIntencionContexto
            {
                Solicitud = solicitud.Solicitud,
                Comandos = solicitud.Comandos.ToList(),
                DatosIntermedios = solicitud.DatosIntermedios.ToList(),
                MetadataEntradasContextoIA = solicitud.MetadataEntradasContextoIA.ToList(),
                CompactacionContextoInicial = solicitud.CompactacionContextoInicial,
                Iteracion = solicitud.Iteracion
            });
            ResultadoIntencionContexto resultado = resultados.Count > 0
                ? resultados.Dequeue()
                : ContextoConversacionServicioTest.ConError("Sin decision configurada.");

            return Task.FromResult(resultado);
        }

        public Task<ResultadoCompactacionIntencionContexto> CompactarAsync(
            SolicitudCompactacionIntencionContexto solicitud,
            CancellationToken cancellationToken)
        {
            Compactaciones.Add(new SolicitudCompactacionIntencionContexto
            {
                Solicitud = solicitud.Solicitud,
                CompactacionContextoInicial = solicitud.CompactacionContextoInicial,
                MetadataEntradasContextoIA = solicitud.MetadataEntradasContextoIA.ToList(),
                Iteracion = solicitud.Iteracion
            });

            return Task.FromResult(
                resultadoCompactacion
                ?? ResultadoCompactacionIntencionContexto.Fallo(
                    "Compactacion no configurada.",
                    ContextoConversacionServicioTest.CrearInformacionTecnicaLlamadaIA(AccionContextoTipo.Error)));
        }
    }

    private sealed class CompactacionContextoConversacionAplicacionFake : ICompactacionContextoConversacionAplicacion
    {
        private readonly CompactacionContextoConversacion? compactacion;

        public CompactacionContextoConversacionAplicacionFake(CompactacionContextoConversacion? compactacion)
        {
            this.compactacion = compactacion;
        }

        public Task<CompactacionContextoConversacion?> ObtenerInicialAsync(
            long idLineaConversacion,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(compactacion);
        }
    }

    private sealed class RegistrarContextoIAAplicacionFake : IRegistrarContextoIAAplicacion
    {
        private long siguienteEntrada = 1;
        private long siguienteMetadata = 1;

        public RegistrarContextoIAAplicacionFake(params MetadataEntradaContextoIA[] entradas)
        {
            Entradas.AddRange(entradas.OrderBy(entrada => entrada.Orden));
            siguienteEntrada = Entradas.Count == 0 ? 1 : Entradas.Max(entrada => entrada.ID) + 1;
            siguienteMetadata = Entradas
                .Where(entrada => entrada.IDInformacionTecnicaLlamadaIA.HasValue)
                .Select(entrada => entrada.IDInformacionTecnicaLlamadaIA!.Value)
                .DefaultIfEmpty()
                .Max() + 1;
        }

        public List<MetadataEntradaContextoIA> Entradas { get; } = [];
        public List<InformacionTecnicaLlamadaIAContexto> InformacionesTecnicasLlamadasIA { get; } = [];
        public List<string> Operaciones { get; } = [];

        public Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasAsync(
            long idLineaConversacion,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MetadataEntradaContextoIA> resultado = Entradas
                .Where(entrada => entrada.IDLineaConversacion == idLineaConversacion)
                .OrderBy(entrada => entrada.Orden)
                .ThenBy(entrada => entrada.ID)
                .ToList();

            return Task.FromResult(resultado);
        }

        public Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasProcesamientoAsync(
            long idLineaConversacion,
            long idProcesamientoInternoMensaje,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MetadataEntradaContextoIA> resultado = Entradas
                .Where(entrada => entrada.IDLineaConversacion == idLineaConversacion
                    && entrada.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .OrderBy(entrada => entrada.Orden)
                .ThenBy(entrada => entrada.ID)
                .ToList();
            return Task.FromResult(resultado);
        }

        public Task<ResultadoRegistrarDecisionContextoIA> RegistrarDecisionAsync(
            SolicitudContextoConversacion solicitud,
            InformacionTecnicaLlamadaIAContexto metadata,
            SolicitudRegistrarMetadataEntradaContextoIA entrada,
            SolicitudPrepararEjecucionComandoContexto? preparacionEjecucion,
            CancellationToken cancellationToken)
        {
            long idMetadata = siguienteMetadata;
            siguienteMetadata++;
            InformacionesTecnicasLlamadasIA.Add(metadata);
            Operaciones.Add($"metadata:{metadata.AccionDecidida}");

            MetadataEntradaContextoIA resultado = CrearEntrada(entrada, idMetadata, metadata);
            Entradas.Add(resultado);
            Operaciones.Add($"entrada:{resultado.IDRolContextoIA}/{resultado.IDTipoEntradaContextoIA}");
            EjecucionComandoContexto? ejecucion = preparacionEjecucion is null
                ? null
                : new EjecucionComandoContexto
                {
                    ID = siguienteEntrada + 1000,
                    IDLineaConversacion = solicitud.IDLineaConversacion,
                    IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                    IDMetadataEntradaDecisionContextoIA = resultado.ID,
                    NumeroIntento = 1,
                    ProveedorEjecucion = preparacionEjecucion.ProveedorEjecucion,
                    CodigoComando = preparacionEjecucion.CodigoComando,
                    ParametrosJson = preparacionEjecucion.ParametrosJson,
                    Estado = EstadosEjecucionComandoContexto.Preparada,
                    Activa = true,
                    ToolCallID = resultado.ToolCallID
                };
            return Task.FromResult(new ResultadoRegistrarDecisionContextoIA
            {
                MetadataEntradaDecision = resultado,
                EjecucionComando = ejecucion
            });
        }

        public Task<MetadataEntradaContextoIA> RegistrarMetadataResultadoComandoAsync(
            long idEjecucionComandoContexto,
            SolicitudRegistrarMetadataEntradaContextoIA entrada,
            ResultadoComandoContexto resultadoComando,
            CancellationToken cancellationToken)
        {
            return RegistrarMetadataEntradaAsync(entrada, cancellationToken);
        }

        public Task<MetadataEntradaContextoIA> RegistrarMetadataEntradaAsync(
            SolicitudRegistrarMetadataEntradaContextoIA solicitud,
            CancellationToken cancellationToken)
        {
            MetadataEntradaContextoIA entrada = CrearEntrada(solicitud, solicitud.IDInformacionTecnicaLlamadaIA, null);
            Entradas.Add(entrada);
            Operaciones.Add($"entrada:{entrada.IDRolContextoIA}/{entrada.IDTipoEntradaContextoIA}");
            return Task.FromResult(entrada);
        }

        private MetadataEntradaContextoIA CrearEntrada(
            SolicitudRegistrarMetadataEntradaContextoIA solicitud,
            long? idMetadata,
            InformacionTecnicaLlamadaIAContexto? metadata)
        {
            int ultimoOrden = Entradas
                .Where(entrada => entrada.IDLineaConversacion == solicitud.IDLineaConversacion)
                .Select(entrada => entrada.Orden)
                .DefaultIfEmpty()
                .Max();
            MetadataEntradaContextoIA entrada = new()
            {
                ID = siguienteEntrada,
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDInformacionTecnicaLlamadaIA = idMetadata,
                Orden = ultimoOrden + 1,
                IDRolContextoIA = solicitud.IDRolContextoIA,
                IDTipoEntradaContextoIA = solicitud.IDTipoEntradaContextoIA,
                Contenido = solicitud.Contenido,
                ToolCallID = solicitud.ToolCallID,
                FechaEntrada = solicitud.FechaEntrada,
                FechaCreacion = DateTime.Now,
                InformacionTecnicaLlamadaIA = metadata
            };

            siguienteEntrada++;
            return entrada;
        }
    }

    private sealed class ProveedorCatalogoComandoContextoFake : IProveedorCatalogoComandoContextoServicio
    {
        private readonly IReadOnlyList<ComandoContexto> comandos;

        public ProveedorCatalogoComandoContextoFake(IReadOnlyList<ComandoContexto> comandos)
        {
            this.comandos = comandos;
        }

        public Task<IReadOnlyList<ComandoContexto>> ObtenerAsync(
            SolicitudContextoConversacion solicitud,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(comandos);
        }
    }

    private sealed class EjecutorComandoContextoFake
    {
        private readonly ResultadoComandoContexto resultado;

        private EjecutorComandoContextoFake(ResultadoComandoContexto resultado)
        {
            this.resultado = resultado;
        }

        public int Llamadas { get; private set; }

        public static EjecutorComandoContextoFake Exitoso(string resultado)
        {
            return new EjecutorComandoContextoFake(ResultadoComandoContexto.Exito(resultado));
        }

        public static EjecutorComandoContextoFake Fallido(string error)
        {
            return new EjecutorComandoContextoFake(ResultadoComandoContexto.Fallo(error));
        }

        public Task<ResultadoComandoContexto> EjecutarAsync(
            SolicitudEjecutarComandoContexto solicitud,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(resultado);
        }
    }

    private sealed class EjecucionComandoContextoAplicacionFake : IEjecucionComandoContextoAplicacion
    {
        private readonly EjecutorComandoContextoFake ejecutor;
        private readonly RegistrarContextoIAAplicacionFake registrar;
        private readonly Task<ResultadoEjecucionComandoContexto?> recuperacion;

        public EjecucionComandoContextoAplicacionFake(
            EjecutorComandoContextoFake ejecutor,
            RegistrarContextoIAAplicacionFake registrar,
            Task<ResultadoEjecucionComandoContexto?>? recuperacion = null)
        {
            this.ejecutor = ejecutor;
            this.registrar = registrar;
            this.recuperacion = recuperacion
                ?? Task.FromResult<ResultadoEjecucionComandoContexto?>(null);
        }

        public string Proveedor => "fake";

        public async Task<ResultadoEjecucionComandoContexto?> ReanudarActivaAsync(
            SolicitudContextoConversacion solicitud,
            IReadOnlyList<ComandoContexto> comandos,
            CancellationToken cancellationToken)
        {
            return await recuperacion.WaitAsync(cancellationToken);
        }

        public async Task<ResultadoEjecucionComandoContexto> EjecutarAsync(
            SolicitudContextoConversacion solicitud,
            EjecucionComandoContexto ejecucion,
            ComandoContexto comando,
            IReadOnlyDictionary<string, string> parametros,
            CancellationToken cancellationToken)
        {
            ResultadoComandoContexto resultado = await ejecutor.EjecutarAsync(
                new SolicitudEjecutarComandoContexto
                {
                    Solicitud = solicitud,
                    Comando = comando,
                    Parametros = parametros
                },
                cancellationToken);
            string contenido = resultado.Exitoso
                ? resultado.Resultado ?? string.Empty
                : resultado.Error ?? "error";
            MetadataEntradaContextoIA entrada = await registrar.RegistrarMetadataResultadoComandoAsync(
                ejecucion.ID,
                new SolicitudRegistrarMetadataEntradaContextoIA
                {
                    IDLineaConversacion = solicitud.IDLineaConversacion,
                    IDMensaje = solicitud.IDMensaje,
                    IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                    IDRolContextoIA = "tool",
                    IDTipoEntradaContextoIA = "resultado_comando",
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
    }

    private sealed class ConsultaMensajesLineaAnteriorFake : IConsultaMensajesLineaConversacionAnteriorAplicacion
    {
        private readonly IReadOnlyDictionary<int, IReadOnlyList<MetadataEntradaContextoIA>> ciclos;

        private ConsultaMensajesLineaAnteriorFake(
            IReadOnlyDictionary<int, IReadOnlyList<MetadataEntradaContextoIA>> ciclos)
        {
            this.ciclos = ciclos;
        }

        public int ConsultasPorPosicion { get; private set; }
        public int ConsultasPorReferencia { get; private set; }

        public static ConsultaMensajesLineaAnteriorFake ConCiclo(params MetadataEntradaContextoIA[] entradas)
        {
            return ConCiclos(entradas);
        }

        public static ConsultaMensajesLineaAnteriorFake ConCiclos(
            params IReadOnlyList<MetadataEntradaContextoIA>[] ciclos)
        {
            return new ConsultaMensajesLineaAnteriorFake(
                ciclos.Select((ciclo, indice) => (Posicion: indice + 1, Ciclo: ciclo))
                    .ToDictionary(elemento => elemento.Posicion, elemento => elemento.Ciclo));
        }

        public static ConsultaMensajesLineaAnteriorFake SinResultados()
        {
            return new ConsultaMensajesLineaAnteriorFake(
                new Dictionary<int, IReadOnlyList<MetadataEntradaContextoIA>>());
        }

        public Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerCicloAsync(
            long idConversacion,
            long idLineaConversacionActual,
            int ciclosHaciaAtras,
            CancellationToken cancellationToken)
        {
            ConsultasPorPosicion++;
            IReadOnlyList<MetadataEntradaContextoIA> ciclo = ciclos.GetValueOrDefault(ciclosHaciaAtras) ?? [];
            return Task.FromResult(ciclo);
        }

        public Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerCicloReferenciadoAsync(
            long idConversacion,
            long idLineaConversacionActual,
            long idLineaConversacionOrigen,
            long idProcesamientoInternoMensaje,
            CancellationToken cancellationToken)
        {
            ConsultasPorReferencia++;
            IReadOnlyList<MetadataEntradaContextoIA> resultado = ciclos.Values
                .SelectMany(ciclo => ciclo)
                .Where(entrada => entrada.IDLineaConversacion == idLineaConversacionOrigen
                    && entrada.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .ToList();
            return Task.FromResult(resultado);
        }
    }
}
