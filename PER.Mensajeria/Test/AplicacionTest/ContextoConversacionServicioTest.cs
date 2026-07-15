using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Entidad.DTO;

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
        Assert.Empty(registrar.Metadatas);
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
        DTOMensajeSaliente mensaje = CrearMensajeSaliente();
        IntencionContextoFake intencion = new(Responder(mensaje));
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Single(resultado.MensajesSalientes);
        MetadataRazonamientoIAContexto metadata = Assert.Single(registrar.Metadatas);
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
        Assert.Single(registrar.Metadatas);
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
            PedirComando("consultar_pedido"),
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
        Assert.Equal(2, registrar.Metadatas.Count);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_comando"),
            ("tool", "resultado_comando"),
            ("assistant", "respuesta_final"));
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
        Assert.Single(registrar.Metadatas);
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
        Assert.Single(registrar.Metadatas);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_comando"));
    }

    [Fact]
    public async Task ResolverAsync_HistorialExitoso_DebeReingresarHistorialAFiltrosEIA()
    {
        FiltroContextoFake filtro = new("filtro", []);
        IntencionContextoFake intencion = new(
            PedirHistorial(),
            Responder(CrearMensajeSaliente()));
        ProveedorHistorialContextoFake historial = ProveedorHistorialContextoFake.Exitoso("historial conversacion");
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [filtro],
            intencion,
            historial: historial,
            registrarContextoIA: registrar);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(1, historial.Llamadas);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, filtro.Llamadas);
        Assert.Contains(intencion.Llamadas[1].DatosIntermedios, dato => dato.Tipo == "historial" && dato.Contenido == "historial conversacion");
        Assert.Equal(2, registrar.Metadatas.Count);
        Assert.Empty(intencion.Compactaciones);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_historial"),
            ("tool", "resultado_historial"),
            ("assistant", "respuesta_final"));
    }

    [Fact]
    public async Task ResolverAsync_LimiteVentana_DebeCompactarContextoAnteriorYRetornarRenovacion()
    {
        EstadoContextoConversacion estadoInicial = new()
        {
            ID = 71,
            IDConversacion = 3,
            IDLineaConversacionOrigen = 70,
            Version = 1,
            Contenido = "snapshot anterior",
            FechaCreacion = DateTime.Now.AddHours(-1)
        };
        RegistrarContextoIAAplicacionFake registrar = new(
            new EntradaContextoIA
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
            CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado));
        IntencionContextoFake intencion = new(
            compactacion,
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado),
                "limite alcanzado",
                DeteccionLimiteVentanaContextoTipo.Estimado));
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar,
            estadoContextoInicial: estadoInicial);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado, resultado.TipoResultado);
        Assert.Same(compactacion, resultado.Compactacion);
        SolicitudCompactacionIntencionContexto solicitudCompactacion = Assert.Single(intencion.Compactaciones);
        Assert.Same(estadoInicial, solicitudCompactacion.EstadoContextoInicial);
        EntradaContextoIA entradaCompactada = Assert.Single(solicitudCompactacion.EntradasContextoIA);
        Assert.Equal(78, entradaCompactada.IDProcesamientoInternoMensaje);
        Assert.DoesNotContain(
            solicitudCompactacion.EntradasContextoIA,
            entrada => entrada.IDProcesamientoInternoMensaje == 1);
        SolicitudIntencionContexto solicitudDecision = Assert.Single(intencion.Llamadas);
        Assert.Same(estadoInicial, solicitudDecision.EstadoContextoInicial);
        Assert.Equal("Compactar", compactacion.Metadata.AccionDecidida);
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
                CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado)),
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado),
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
            new EntradaContextoIA
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
                CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado)),
            ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado),
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
    public async Task ResolverAsync_MetadataCompactacionIncompleta_DebeImpedirRenovacion()
    {
        MetadataRazonamientoIAContexto metadataCompactacion = CrearMetadata(
            AccionContextoTipo.LimiteVentanaAlcanzado);
        ResultadoCompactacionIntencionContexto compactacion = ResultadoCompactacionIntencionContexto.Exito(
            "snapshot",
            metadataCompactacion);
        metadataCompactacion.Adaptador = string.Empty;
        RegistrarContextoIAAplicacionFake registrar = new(
            new EntradaContextoIA
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
                CrearMetadata(AccionContextoTipo.LimiteVentanaAlcanzado),
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
            PedirHistorial(),
            PedirHistorial(),
            PedirHistorial());
        ProveedorHistorialContextoFake historial = ProveedorHistorialContextoFake.Exitoso("historial");
        RegistrarContextoIAAplicacionFake registrar = new();
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            historial: historial,
            registrarContextoIA: registrar,
            maximoIteraciones: 2);

        ResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(ResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("maximo de iteraciones", resultado.Error);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, historial.Llamadas);
        Assert.Equal(2, registrar.Metadatas.Count);
        AssertEntradas(
            registrar,
            ("user", "mensaje_entrada"),
            ("assistant", "decision_historial"),
            ("tool", "resultado_historial"),
            ("assistant", "decision_historial"),
            ("tool", "resultado_historial"));
    }

    [Fact]
    public async Task ResolverAsync_DebeEnviarALaIALasEntradasDeTodaLaLineaConMetadata()
    {
        MetadataRazonamientoIAContexto metadataAnterior = CrearMetadata(AccionContextoTipo.Responder);
        metadataAnterior.Iteracion = 1;
        metadataAnterior.Reasoning = "razonamiento anterior";
        RegistrarContextoIAAplicacionFake registrar = new(
            new EntradaContextoIA
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
            new EntradaContextoIA
            {
                ID = 11,
                IDLineaConversacion = 4,
                IDMensaje = 20,
                IDProcesamientoInternoMensaje = 30,
                IDMetadataRazonamientoIA = 40,
                Orden = 2,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "respuesta anterior",
                FechaEntrada = DateTime.Now.AddMinutes(-4),
                Metadata = metadataAnterior
            });
        IntencionContextoFake intencion = new(NoResponder());
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            registrarContextoIA: registrar);

        await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        SolicitudIntencionContexto solicitudIA = Assert.Single(intencion.Llamadas);
        Assert.Equal(3, solicitudIA.EntradasContextoIA.Count);
        Assert.Equal([1, 2, 3], solicitudIA.EntradasContextoIA.Select(entrada => entrada.Orden));
        EntradaContextoIA entradaAnterior = solicitudIA.EntradasContextoIA[1];
        Assert.NotNull(entradaAnterior.Metadata);
        Assert.Equal("razonamiento anterior", entradaAnterior.Metadata.Reasoning);
        Assert.Equal(2, solicitudIA.EntradasContextoIA.Count(
            entrada => entrada.IDProcesamientoInternoMensaje != 1));
    }

    [Fact]
    public async Task ResolverAsync_ReinicioConResultadoComandoPersistido_DebeContinuarSinReejecutarComando()
    {
        MetadataRazonamientoIAContexto metadataComando = CrearMetadata(AccionContextoTipo.Comando);
        metadataComando.Iteracion = 1;
        RegistrarContextoIAAplicacionFake registrar = new(
            new EntradaContextoIA
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
            new EntradaContextoIA
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
            new EntradaContextoIA
            {
                ID = 12,
                IDLineaConversacion = 4,
                IDMensaje = 2,
                IDProcesamientoInternoMensaje = 1,
                IDMetadataRazonamientoIA = 50,
                Orden = 3,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "decision_comando",
                Contenido = "comando:consultar_pedido",
                FechaEntrada = DateTime.Now.AddMinutes(-1),
                Metadata = metadataComando
            },
            new EntradaContextoIA
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

    [Theory]
    [InlineData("proveedor")]
    [InlineData("modelo")]
    [InlineData("adaptador")]
    public async Task ResolverAsync_MetadataIncompleta_DebeFallarAntesDePersistir(string campo)
    {
        MetadataRazonamientoIAContexto metadata = CrearMetadata(AccionContextoTipo.NoResponder);
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

        Assert.Empty(registrar.Metadatas);
        AssertEntradas(registrar, ("user", "mensaje_entrada"));
    }

    [Fact]
    public void ResultadoIntencionContexto_DebeExigirMetadataYContenido()
    {
        MetadataRazonamientoIAContexto metadataSinProveedor = CrearMetadata(AccionContextoTipo.NoResponder);
        metadataSinProveedor.Proveedor = string.Empty;
        MetadataRazonamientoIAContexto metadataSinModelo = CrearMetadata(AccionContextoTipo.NoResponder);
        metadataSinModelo.Modelo = string.Empty;
        MetadataRazonamientoIAContexto metadataSinAdaptador = CrearMetadata(AccionContextoTipo.NoResponder);
        metadataSinAdaptador.Adaptador = string.Empty;

        Assert.Throws<ArgumentNullException>(
            () => ResultadoIntencionContexto.NoResponder(null!, "contenido"));
        Assert.Throws<ArgumentException>(
            () => ResultadoIntencionContexto.NoResponder(
                CrearMetadata(AccionContextoTipo.NoResponder),
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
        ProveedorHistorialContextoFake? historial = null,
        RegistrarContextoIAAplicacionFake? registrarContextoIA = null,
        EstadoContextoConversacion? estadoContextoInicial = null,
        int maximoIteraciones = 5)
    {
        return new ContextoConversacionServicio(
            filtros,
            intencion,
            new ProveedorCatalogoComandoContextoFake(catalogo ?? []),
            ejecutor ?? EjecutorComandoContextoFake.Exitoso("resultado"),
            historial ?? ProveedorHistorialContextoFake.Exitoso("historial"),
            registrarContextoIA ?? new RegistrarContextoIAAplicacionFake(),
            new EstadoContextoConversacionAplicacionFake(estadoContextoInicial),
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

    private static DTOMensajeSaliente CrearMensajeSaliente()
    {
        return new DTOMensajeSaliente
        {
            IDConversacion = 3,
            IDLineaConversacion = 4,
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
            CrearMetadata(AccionContextoTipo.NoResponder),
            "no_responder");
    }

    private static ResultadoIntencionContexto Responder(DTOMensajeSaliente mensaje)
    {
        return ResultadoIntencionContexto.Responder(
            CrearMetadata(AccionContextoTipo.Responder),
            mensaje.Contenido ?? "respuesta",
            mensaje);
    }

    private static ResultadoIntencionContexto PedirComando(
        string codigoComando,
        Dictionary<string, string>? parametros = null)
    {
        return ResultadoIntencionContexto.PedirComando(
            CrearMetadata(AccionContextoTipo.Comando),
            $"comando:{codigoComando}",
            codigoComando,
            parametros);
    }

    private static ResultadoIntencionContexto PedirHistorial()
    {
        return ResultadoIntencionContexto.PedirHistorial(
            CrearMetadata(AccionContextoTipo.Historial),
            "historial");
    }

    private static ResultadoIntencionContexto ConError(string error)
    {
        return ResultadoIntencionContexto.ConError(
            CrearMetadata(AccionContextoTipo.Error),
            error,
            error);
    }

    private static MetadataRazonamientoIAContexto CrearMetadata(AccionContextoTipo accion)
    {
        return new MetadataRazonamientoIAContexto
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
                EntradasContextoIA = solicitud.EntradasContextoIA.ToList(),
                EstadoContextoInicial = solicitud.EstadoContextoInicial,
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
                EstadoContextoInicial = solicitud.EstadoContextoInicial,
                EntradasContextoIA = solicitud.EntradasContextoIA.ToList(),
                Iteracion = solicitud.Iteracion
            });

            return Task.FromResult(
                resultadoCompactacion
                ?? ResultadoCompactacionIntencionContexto.Fallo(
                    "Compactacion no configurada.",
                    ContextoConversacionServicioTest.CrearMetadata(AccionContextoTipo.Error)));
        }
    }

    private sealed class EstadoContextoConversacionAplicacionFake : IEstadoContextoConversacionAplicacion
    {
        private readonly EstadoContextoConversacion? estado;

        public EstadoContextoConversacionAplicacionFake(EstadoContextoConversacion? estado)
        {
            this.estado = estado;
        }

        public Task<EstadoContextoConversacion?> ObtenerInicialAsync(
            long idLineaConversacion,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(estado);
        }
    }

    private sealed class RegistrarContextoIAAplicacionFake : IRegistrarContextoIAAplicacion
    {
        private long siguienteEntrada = 1;
        private long siguienteMetadata = 1;

        public RegistrarContextoIAAplicacionFake(params EntradaContextoIA[] entradas)
        {
            Entradas.AddRange(entradas.OrderBy(entrada => entrada.Orden));
            siguienteEntrada = Entradas.Count == 0 ? 1 : Entradas.Max(entrada => entrada.ID) + 1;
            siguienteMetadata = Entradas
                .Where(entrada => entrada.IDMetadataRazonamientoIA.HasValue)
                .Select(entrada => entrada.IDMetadataRazonamientoIA!.Value)
                .DefaultIfEmpty()
                .Max() + 1;
        }

        public List<EntradaContextoIA> Entradas { get; } = [];
        public List<MetadataRazonamientoIAContexto> Metadatas { get; } = [];
        public List<string> Operaciones { get; } = [];

        public Task<IReadOnlyList<EntradaContextoIA>> ObtenerEntradasAsync(
            long idLineaConversacion,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<EntradaContextoIA> resultado = Entradas
                .Where(entrada => entrada.IDLineaConversacion == idLineaConversacion)
                .OrderBy(entrada => entrada.Orden)
                .ThenBy(entrada => entrada.ID)
                .ToList();

            return Task.FromResult(resultado);
        }

        public Task<EntradaContextoIA> RegistrarDecisionAsync(
            SolicitudContextoConversacion solicitud,
            MetadataRazonamientoIAContexto metadata,
            SolicitudRegistrarEntradaContextoIA entrada,
            CancellationToken cancellationToken)
        {
            long idMetadata = siguienteMetadata;
            siguienteMetadata++;
            Metadatas.Add(metadata);
            Operaciones.Add($"metadata:{metadata.AccionDecidida}");

            EntradaContextoIA resultado = CrearEntrada(entrada, idMetadata, metadata);
            Entradas.Add(resultado);
            Operaciones.Add($"entrada:{resultado.IDRolContextoIA}/{resultado.IDTipoEntradaContextoIA}");
            return Task.FromResult(resultado);
        }

        public Task<EntradaContextoIA> RegistrarEntradaAsync(
            SolicitudRegistrarEntradaContextoIA solicitud,
            CancellationToken cancellationToken)
        {
            EntradaContextoIA entrada = CrearEntrada(solicitud, solicitud.IDMetadataRazonamientoIA, null);
            Entradas.Add(entrada);
            Operaciones.Add($"entrada:{entrada.IDRolContextoIA}/{entrada.IDTipoEntradaContextoIA}");
            return Task.FromResult(entrada);
        }

        private EntradaContextoIA CrearEntrada(
            SolicitudRegistrarEntradaContextoIA solicitud,
            long? idMetadata,
            MetadataRazonamientoIAContexto? metadata)
        {
            int ultimoOrden = Entradas
                .Where(entrada => entrada.IDLineaConversacion == solicitud.IDLineaConversacion)
                .Select(entrada => entrada.Orden)
                .DefaultIfEmpty()
                .Max();
            EntradaContextoIA entrada = new()
            {
                ID = siguienteEntrada,
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDMetadataRazonamientoIA = idMetadata,
                Orden = ultimoOrden + 1,
                IDRolContextoIA = solicitud.IDRolContextoIA,
                IDTipoEntradaContextoIA = solicitud.IDTipoEntradaContextoIA,
                Contenido = solicitud.Contenido,
                ToolCallID = solicitud.ToolCallID,
                FechaEntrada = solicitud.FechaEntrada,
                Metadata = metadata
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

    private sealed class EjecutorComandoContextoFake : IEjecutorComandoContextoServicio
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

    private sealed class ProveedorHistorialContextoFake : IProveedorHistorialContextoServicio
    {
        private readonly ResultadoHistorialContexto resultado;

        private ProveedorHistorialContextoFake(ResultadoHistorialContexto resultado)
        {
            this.resultado = resultado;
        }

        public int Llamadas { get; private set; }

        public static ProveedorHistorialContextoFake Exitoso(string historial)
        {
            return new ProveedorHistorialContextoFake(ResultadoHistorialContexto.Exito(historial));
        }

        public Task<ResultadoHistorialContexto> ObtenerAsync(
            SolicitudContextoConversacion solicitud,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(resultado);
        }
    }
}
