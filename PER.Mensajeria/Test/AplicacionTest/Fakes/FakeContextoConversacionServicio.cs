using PER.Mensajeria.API.Contexto;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest.Fakes;

public class FakeContextoConversacionServicio : IContextoConversacionServicio
{
    private readonly DTOResultadoContextoConversacion? resultado;
    private readonly Exception? excepcion;

    private FakeContextoConversacionServicio(DTOResultadoContextoConversacion? resultado, Exception? excepcion)
    {
        this.resultado = resultado;
        this.excepcion = excepcion;
    }

    public bool Ejecutado { get; private set; }
    public DTOContextoConversacionSolicitud? SolicitudRecibida { get; private set; }
    public int PasosInternosSimulados { get; private set; }

    public static FakeContextoConversacionServicio ConSalidas(params DTOMensajeSaliente[] mensajesSalientes)
    {
        return new FakeContextoConversacionServicio(new DTOResultadoContextoConversacion
        {
            TipoResultado = DTOResultadoContextoConversacionTipo.ConSalidas,
            MensajesSalientes = mensajesSalientes.ToList()
        }, null);
    }

    public static FakeContextoConversacionServicio SinSalidas()
    {
        return new FakeContextoConversacionServicio(new DTOResultadoContextoConversacion
        {
            TipoResultado = DTOResultadoContextoConversacionTipo.SinSalidas
        }, null);
    }

    public static FakeContextoConversacionServicio ConError(string error)
    {
        return new FakeContextoConversacionServicio(new DTOResultadoContextoConversacion
        {
            TipoResultado = DTOResultadoContextoConversacionTipo.Error,
            Error = error
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

    public Task<DTOResultadoContextoConversacion> ResolverAsync(
        DTOContextoConversacionSolicitud solicitud,
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
