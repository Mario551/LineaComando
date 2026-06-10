using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Contexto;

namespace PER.Mensajeria.Servicio.Orquestador;

public class OrquestadorContextoServicio : IOrquestadorContextoServicio
{
    private readonly IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion;
    private readonly IContextoConversacionActivoServicio contextoConversacionActivoServicio;

    public OrquestadorContextoServicio(IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion)
        : this(orquestarMensajeEntradaAplicacion, new ContextoConversacionActivoServicio())
    {
    }

    public OrquestadorContextoServicio(
        IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion,
        IContextoConversacionActivoServicio contextoConversacionActivoServicio)
    {
        this.orquestarMensajeEntradaAplicacion = orquestarMensajeEntradaAplicacion;
        this.contextoConversacionActivoServicio = contextoConversacionActivoServicio;
    }

    public Task ProcesarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken)
    {
        return contextoConversacionActivoServicio.EjecutarAsync(
            eventoMensajeria.IDConversacion,
            token => orquestarMensajeEntradaAplicacion.EjecutarAsync(
                eventoMensajeria.IDProcesamientoInternoMensaje,
                token),
            cancellationToken);
    }
}
