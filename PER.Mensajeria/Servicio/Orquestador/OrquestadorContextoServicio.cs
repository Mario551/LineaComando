using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Contexto;
using PER.Mensajeria.Servicio.Mensaje;

namespace PER.Mensajeria.Servicio.Orquestador;

public class OrquestadorContextoServicio : IOrquestadorContextoServicio
{
    private readonly IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion;
    private readonly IContextoConversacionActivoServicio contextoConversacionActivoServicio;
    private readonly IMensajeServicio mensajeServicio;
    private readonly ILogger<OrquestadorContextoServicio> logger;

    public OrquestadorContextoServicio(
        IOrquestarMensajeEntradaAplicacion orquestarMensajeEntradaAplicacion,
        IContextoConversacionActivoServicio contextoConversacionActivoServicio,
        IMensajeServicio mensajeServicio,
        ILogger<OrquestadorContextoServicio> logger)
    {
        this.orquestarMensajeEntradaAplicacion = orquestarMensajeEntradaAplicacion;
        this.contextoConversacionActivoServicio = contextoConversacionActivoServicio;
        this.mensajeServicio = mensajeServicio;
        this.logger = logger;
    }

    public async Task ProcesarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Inicia orquestacion de contexto. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
            eventoMensajeria.IDProcesamientoInternoMensaje,
            eventoMensajeria.IDConversacion);

        try
        {
            await contextoConversacionActivoServicio.EjecutarAsync(
                eventoMensajeria.IDConversacion,
                token => ProcesarEventoAsync(eventoMensajeria, token),
                cancellationToken);

            logger.LogInformation(
                "Finaliza orquestacion de contexto. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
                eventoMensajeria.IDProcesamientoInternoMensaje,
                eventoMensajeria.IDConversacion);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                excepcion,
                "Error en orquestacion de contexto. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
                eventoMensajeria.IDProcesamientoInternoMensaje,
                eventoMensajeria.IDConversacion);
            throw;
        }
    }

    private async Task ProcesarEventoAsync(
        EventoMensajeria eventoMensajeria,
        CancellationToken cancellationToken)
    {
        ResultadoOrquestarMensajeEntrada resultado = await orquestarMensajeEntradaAplicacion.EjecutarAsync(
            eventoMensajeria.IDProcesamientoInternoMensaje,
            cancellationToken);

        if (resultado.Tipo != ResultadoOrquestarMensajeEntradaTipo.RenovarLinea)
        {
            return;
        }

        await mensajeServicio.RenovarLineaContextoAsync(
            new SolicitudRenovarLineaContexto
            {
                IDProcesamientoInternoMensaje = eventoMensajeria.IDProcesamientoInternoMensaje,
                IDMensaje = eventoMensajeria.IDMensaje,
                IDConversacion = eventoMensajeria.IDConversacion,
                IDLineaConversacionOrigen = eventoMensajeria.IDLineaConversacion,
                Compactacion = resultado.Compactacion
                    ?? throw new InvalidOperationException("La renovacion de linea requiere una compactacion.")
            },
            cancellationToken);
    }
}
