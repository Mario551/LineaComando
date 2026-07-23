using PER.Mensajeria.Aplicacion.Contexto;

namespace AplicacionTest.Fakes;

public class FakeContextoConversacionServicio : IContextoConversacionServicio
{
    private readonly ResultadoContextoConversacion? resultado;
    private readonly Exception? excepcion;
    private readonly CancellationTokenSource? fuenteCancelacion;

    private FakeContextoConversacionServicio(
        ResultadoContextoConversacion? resultado,
        Exception? excepcion,
        CancellationTokenSource? fuenteCancelacion = null)
    {
        this.resultado = resultado;
        this.excepcion = excepcion;
        this.fuenteCancelacion = fuenteCancelacion;
    }

    public bool Ejecutado { get; private set; }
    public SolicitudContextoConversacion? SolicitudRecibida { get; private set; }
    public int PasosInternosSimulados { get; private set; }
    public Action? AntesDeResolver { get; set; }

    public static FakeContextoConversacionServicio ConSalidas(params MensajeSalienteContexto[] mensajesSalientes)
    {
        return new FakeContextoConversacionServicio(new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.ConSalidas,
            MensajesSalientes = mensajesSalientes.ToList()
        }, null);
    }

    public static FakeContextoConversacionServicio SinSalidas()
    {
        return new FakeContextoConversacionServicio(new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.SinSalidas
        }, null);
    }

    public static FakeContextoConversacionServicio ConError(string error)
    {
        return new FakeContextoConversacionServicio(new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.Error,
            Error = error
        }, null);
    }

    public static FakeContextoConversacionServicio LimiteVentana(
        ResultadoCompactacionIntencionContexto compactacion)
    {
        return new FakeContextoConversacionServicio(new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado,
            Compactacion = compactacion
        }, null);
    }

    public static FakeContextoConversacionServicio ConExcepcion(Exception excepcion)
    {
        return new FakeContextoConversacionServicio(null, excepcion);
    }

    public static FakeContextoConversacionServicio ConCancelacion(CancellationTokenSource fuenteCancelacion)
    {
        return new FakeContextoConversacionServicio(null, null, fuenteCancelacion);
    }

    public static FakeContextoConversacionServicio ConComandoIntermedio(MensajeSalienteContexto mensajeSaliente)
    {
        FakeContextoConversacionServicio fake = ConSalidas(mensajeSaliente);
        fake.PasosInternosSimulados = 1;
        return fake;
    }

    public static FakeContextoConversacionServicio ConConsultaMensajesAnterioresIntermedia(MensajeSalienteContexto mensajeSaliente)
    {
        FakeContextoConversacionServicio fake = ConSalidas(mensajeSaliente);
        fake.PasosInternosSimulados = 1;
        return fake;
    }

    public Task<ResultadoContextoConversacion> ResolverAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken)
    {
        AntesDeResolver?.Invoke();
        Ejecutado = true;
        SolicitudRecibida = solicitud;

        if (fuenteCancelacion is not null)
        {
            fuenteCancelacion.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }

        if (excepcion is not null)
        {
            throw excepcion;
        }

        return Task.FromResult(resultado!);
    }
}
