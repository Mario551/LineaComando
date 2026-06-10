using PER.Mensajeria.API.Contexto;
using PER.Mensajeria.Entidad.DTO;

namespace APITest;

public class ContextoConversacionServicioTest
{
    [Fact]
    public async Task ResolverAsync_FiltrosConfigurados_DebeEjecutarlosEnOrden()
    {
        List<string> orden = [];
        FiltroContextoFake filtroA = new("A", orden);
        FiltroContextoFake filtroB = new("B", orden);
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.NoResponder());
        ContextoConversacionServicio servicio = CrearServicio([filtroA, filtroB], intencion);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.SinSalidas, resultado.TipoResultado);
        Assert.Equal(["A", "B"], orden);
    }

    [Fact]
    public async Task ResolverAsync_FiltroRetornaError_DebeCortarFlujoSinEjecutarIA()
    {
        List<string> orden = [];
        FiltroContextoFake filtroA = new("A", orden);
        FiltroContextoFake filtroB = new("B", orden, "Filtro B no pasa.");
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.NoResponder());
        ContextoConversacionServicio servicio = CrearServicio([filtroA, filtroB], intencion);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("Filtro B no pasa", resultado.Error);
        Assert.Equal(["A", "B"], orden);
        Assert.Empty(intencion.Llamadas);
    }

    [Fact]
    public async Task ResolverAsync_IADebeRecibirCatalogoComandos()
    {
        DTOComandoContexto comando = CrearComando("consultar_pedido");
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.NoResponder());
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [comando]);

        await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        DTOIntencionContextoSolicitud solicitudIA = Assert.Single(intencion.Llamadas);
        DTOComandoContexto comandoRecibido = Assert.Single(solicitudIA.Comandos);
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
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.Responder(mensaje));
        ContextoConversacionServicio servicio = CrearServicio([new FiltroContextoFake("A", [])], intencion);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Single(resultado.MensajesSalientes);
    }

    [Fact]
    public async Task ResolverAsync_ResultadoNoResponder_DebeRetornarSinSalidas()
    {
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.NoResponder());
        ContextoConversacionServicio servicio = CrearServicio([new FiltroContextoFake("A", [])], intencion);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.SinSalidas, resultado.TipoResultado);
        Assert.Empty(resultado.MensajesSalientes);
    }

    [Fact]
    public async Task ResolverAsync_ComandoExitoso_DebeReingresarResultadoAFiltrosEIA()
    {
        List<string> orden = [];
        FiltroContextoFake filtro = new("filtro", orden);
        IntencionContextoFake intencion = new(
            DTOIntencionContextoResultado.PedirComando("consultar_pedido"),
            DTOIntencionContextoResultado.Responder(CrearMensajeSaliente()));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Exitoso("pedido encontrado");
        ContextoConversacionServicio servicio = CrearServicio(
            [filtro],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(1, ejecutor.Llamadas);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, filtro.Llamadas);
        Assert.Contains(intencion.Llamadas[1].DatosIntermedios, dato => dato.Tipo == "comando" && dato.Contenido == "pedido encontrado");
    }

    [Fact]
    public async Task ResolverAsync_ComandoInvalidoLimitantes_DebeRetornarErrorSinInvocarCola()
    {
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.PedirComando("comando_no_autorizado"));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Exitoso("no debe ejecutarse");
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("Comando no autorizado", resultado.Error);
        Assert.Equal(0, ejecutor.Llamadas);
    }

    [Fact]
    public async Task ResolverAsync_ColaComandoFalla_DebeRetornarErrorControlado()
    {
        IntencionContextoFake intencion = new(DTOIntencionContextoResultado.PedirComando("consultar_pedido"));
        EjecutorComandoContextoFake ejecutor = EjecutorComandoContextoFake.Fallido("cola no disponible");
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            catalogo: [CrearComando("consultar_pedido")],
            ejecutor: ejecutor);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("cola no disponible", resultado.Error);
        Assert.Equal(1, ejecutor.Llamadas);
        Assert.Single(intencion.Llamadas);
    }

    [Fact]
    public async Task ResolverAsync_HistorialExitoso_DebeReingresarHistorialAFiltrosEIA()
    {
        FiltroContextoFake filtro = new("filtro", []);
        IntencionContextoFake intencion = new(
            DTOIntencionContextoResultado.PedirHistorial(),
            DTOIntencionContextoResultado.Responder(CrearMensajeSaliente()));
        ProveedorHistorialContextoFake historial = ProveedorHistorialContextoFake.Exitoso("historial conversacion");
        ContextoConversacionServicio servicio = CrearServicio(
            [filtro],
            intencion,
            historial: historial);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.ConSalidas, resultado.TipoResultado);
        Assert.Equal(1, historial.Llamadas);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, filtro.Llamadas);
        Assert.Contains(intencion.Llamadas[1].DatosIntermedios, dato => dato.Tipo == "historial" && dato.Contenido == "historial conversacion");
    }

    [Fact]
    public async Task ResolverAsync_MaximoIteraciones_DebeCortarCicloInfinito()
    {
        IntencionContextoFake intencion = new(
            DTOIntencionContextoResultado.PedirHistorial(),
            DTOIntencionContextoResultado.PedirHistorial(),
            DTOIntencionContextoResultado.PedirHistorial());
        ProveedorHistorialContextoFake historial = ProveedorHistorialContextoFake.Exitoso("historial");
        ContextoConversacionServicio servicio = CrearServicio(
            [new FiltroContextoFake("A", [])],
            intencion,
            historial: historial,
            maximoIteraciones: 2);

        DTOResultadoContextoConversacion resultado = await servicio.ResolverAsync(CrearSolicitud(), CancellationToken.None);

        Assert.Equal(DTOResultadoContextoConversacionTipo.Error, resultado.TipoResultado);
        Assert.Contains("maximo de iteraciones", resultado.Error);
        Assert.Equal(2, intencion.Llamadas.Count);
        Assert.Equal(2, historial.Llamadas);
    }

    private static ContextoConversacionServicio CrearServicio(
        IReadOnlyList<IFiltroContextoConversacion> filtros,
        IntencionContextoFake intencion,
        IReadOnlyList<DTOComandoContexto>? catalogo = null,
        EjecutorComandoContextoFake? ejecutor = null,
        ProveedorHistorialContextoFake? historial = null,
        int maximoIteraciones = 5)
    {
        return new ContextoConversacionServicio(
            filtros,
            intencion,
            new ProveedorCatalogoComandoContextoFake(catalogo ?? []),
            ejecutor ?? EjecutorComandoContextoFake.Exitoso("resultado"),
            historial ?? ProveedorHistorialContextoFake.Exitoso("historial"),
            new ConfiguracionContextoConversacion
            {
                MaximoIteraciones = maximoIteraciones
            });
    }

    private static DTOContextoConversacionSolicitud CrearSolicitud()
    {
        return new DTOContextoConversacionSolicitud
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

    private static DTOComandoContexto CrearComando(string codigo)
    {
        return new DTOComandoContexto
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

        public Task<DTOResultadoFiltroContexto> EjecutarAsync(
            DTOContextoConversacionEstado estado,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            orden.Add(nombre);

            if (error is not null)
            {
                return Task.FromResult(DTOResultadoFiltroContexto.DetenerConError(error));
            }

            return Task.FromResult(DTOResultadoFiltroContexto.ContinuarFlujo());
        }
    }

    private sealed class IntencionContextoFake : IIntencionContextoConversacionServicio
    {
        private readonly Queue<DTOIntencionContextoResultado> resultados;

        public IntencionContextoFake(params DTOIntencionContextoResultado[] resultados)
        {
            this.resultados = new Queue<DTOIntencionContextoResultado>(resultados);
        }

        public List<DTOIntencionContextoSolicitud> Llamadas { get; } = [];

        public Task<DTOIntencionContextoResultado> DecidirAsync(
            DTOIntencionContextoSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            Llamadas.Add(solicitud);
            DTOIntencionContextoResultado resultado = resultados.Count > 0
                ? resultados.Dequeue()
                : DTOIntencionContextoResultado.ConError("Sin decision configurada.");

            return Task.FromResult(resultado);
        }
    }

    private sealed class ProveedorCatalogoComandoContextoFake : IProveedorCatalogoComandoContextoServicio
    {
        private readonly IReadOnlyList<DTOComandoContexto> comandos;

        public ProveedorCatalogoComandoContextoFake(IReadOnlyList<DTOComandoContexto> comandos)
        {
            this.comandos = comandos;
        }

        public Task<IReadOnlyList<DTOComandoContexto>> ObtenerAsync(
            DTOContextoConversacionSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(comandos);
        }
    }

    private sealed class EjecutorComandoContextoFake : IEjecutorComandoContextoServicio
    {
        private readonly DTOResultadoComandoContexto resultado;

        private EjecutorComandoContextoFake(DTOResultadoComandoContexto resultado)
        {
            this.resultado = resultado;
        }

        public int Llamadas { get; private set; }

        public static EjecutorComandoContextoFake Exitoso(string resultado)
        {
            return new EjecutorComandoContextoFake(DTOResultadoComandoContexto.Exito(resultado));
        }

        public static EjecutorComandoContextoFake Fallido(string error)
        {
            return new EjecutorComandoContextoFake(DTOResultadoComandoContexto.Fallo(error));
        }

        public Task<DTOResultadoComandoContexto> EjecutarAsync(
            DTOEjecutarComandoContextoSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(resultado);
        }
    }

    private sealed class ProveedorHistorialContextoFake : IProveedorHistorialContextoServicio
    {
        private readonly DTOResultadoHistorialContexto resultado;

        private ProveedorHistorialContextoFake(DTOResultadoHistorialContexto resultado)
        {
            this.resultado = resultado;
        }

        public int Llamadas { get; private set; }

        public static ProveedorHistorialContextoFake Exitoso(string historial)
        {
            return new ProveedorHistorialContextoFake(DTOResultadoHistorialContexto.Exito(historial));
        }

        public Task<DTOResultadoHistorialContexto> ObtenerAsync(
            DTOContextoConversacionSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(resultado);
        }
    }
}
