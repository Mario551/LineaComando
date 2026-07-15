using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest.Fakes;

public class FakeContextoConversacionServicio : IContextoConversacionServicio
{
    private readonly ResultadoContextoConversacion? resultado;
    private readonly Exception? excepcion;

    private FakeContextoConversacionServicio(ResultadoContextoConversacion? resultado, Exception? excepcion)
    {
        this.resultado = resultado;
        this.excepcion = excepcion;
    }

    public bool Ejecutado { get; private set; }
    public SolicitudContextoConversacion? SolicitudRecibida { get; private set; }
    public int PasosInternosSimulados { get; private set; }

    public static FakeContextoConversacionServicio ConSalidas(params DTOMensajeSaliente[] mensajesSalientes)
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

    public static FakeContextoConversacionServicio ConComandoIntermedio(DTOMensajeSaliente mensajeSaliente)
    {
        FakeContextoConversacionServicio fake = ConSalidas(mensajeSaliente);
        fake.PasosInternosSimulados = 1;
        return fake;
    }

    public static FakeContextoConversacionServicio ConHistorialIntermedio(DTOMensajeSaliente mensajeSaliente)
    {
        FakeContextoConversacionServicio fake = ConSalidas(mensajeSaliente);
        fake.PasosInternosSimulados = 1;
        return fake;
    }

    public Task<ResultadoContextoConversacion> ResolverAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken)
    {
        Ejecutado = true;
        SolicitudRecibida = solicitud;

        if (excepcion is not null)
        {
            throw excepcion;
        }

        return Task.FromResult(resultado!);
    }
}
