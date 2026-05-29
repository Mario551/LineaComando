using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Servicio.Cola;

namespace PER.Mensajeria.Servicio.Orquestador;

public class OrquestadorContextoServicio : IOrquestadorContextoServicio
{
    private readonly IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion;

    public OrquestadorContextoServicio(IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion)
    {
        this.orquestarMensajeEntradaAplicacion = orquestarMensajeEntradaAplicacion;
    }

    public Task ProcesarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken)
    {
        return orquestarMensajeEntradaAplicacion.EjecutarAsync(eventoMensajeria.IDProcesamientoInternoMensaje, cancellationToken);
    }
}
