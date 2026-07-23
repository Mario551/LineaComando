using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;

namespace PER.Mensajeria.Servicio.Orquestador;

internal sealed class ProcesadorConversacionServicio
{
    private readonly long idConversacion;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly SemaphoreSlim limiteConversaciones;
    private readonly Action<long, ProcesadorConversacionServicio> retirarProcesador;
    private readonly CancellationToken cancellationToken;
    private readonly ILogger<OrquestadorContextoServicio> logger;
    private readonly object sincronizacion = new();
    private readonly Queue<EventoMensajeriaEntrada> eventos = new();
    private readonly HashSet<long> idsProcesamientos = [];
    private Task? tareaProcesamiento;
    private bool cerrado;

    public ProcesadorConversacionServicio(
        long idConversacion,
        IServiceScopeFactory serviceScopeFactory,
        SemaphoreSlim limiteConversaciones,
        Action<long, ProcesadorConversacionServicio> retirarProcesador,
        CancellationToken cancellationToken,
        ILogger<OrquestadorContextoServicio> logger)
    {
        this.idConversacion = idConversacion;
        this.serviceScopeFactory = serviceScopeFactory;
        this.limiteConversaciones = limiteConversaciones;
        this.retirarProcesador = retirarProcesador;
        this.cancellationToken = cancellationToken;
        this.logger = logger;
    }

    public Task Finalizacion
    {
        get
        {
            lock (sincronizacion)
            {
                return tareaProcesamiento ?? Task.CompletedTask;
            }
        }
    }

    public bool IntentarEncolar(EventoMensajeriaEntrada eventoMensajeria)
    {
        lock (sincronizacion)
        {
            if (cerrado)
            {
                return false;
            }

            if (!idsProcesamientos.Add(eventoMensajeria.IDProcesamientoInternoMensaje))
            {
                logger.LogDebug(
                    "Evento de contexto duplicado ignorado. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
                    eventoMensajeria.IDProcesamientoInternoMensaje,
                    eventoMensajeria.IDConversacion);
                return true;
            }

            eventos.Enqueue(eventoMensajeria);
            tareaProcesamiento ??= ProcesarColaAsync();
            return true;
        }
    }

    private async Task ProcesarColaAsync()
    {
        bool cupoAdquirido = false;

        try
        {
            await Task.Yield();
            await limiteConversaciones.WaitAsync(cancellationToken);
            cupoAdquirido = true;

            while (IntentarObtenerSiguiente(out EventoMensajeriaEntrada eventoMensajeria))
            {
                try
                {
                    await ProcesarEventoConRenovacionAsync(eventoMensajeria, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception excepcion)
                {
                    logger.LogError(
                        excepcion,
                        "Error aislado procesando evento de contexto. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
                        eventoMensajeria.IDProcesamientoInternoMensaje,
                        eventoMensajeria.IDConversacion);
                }
                finally
                {
                    lock (sincronizacion)
                    {
                        idsProcesamientos.Remove(eventoMensajeria.IDProcesamientoInternoMensaje);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Procesador de conversacion cancelado durante el apagado. IDConversacion={IDConversacion}",
                idConversacion);
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Error no controlado en procesador de conversacion. IDConversacion={IDConversacion}",
                idConversacion);
        }
        finally
        {
            Cerrar();

            if (cupoAdquirido)
            {
                limiteConversaciones.Release();
            }

            retirarProcesador(idConversacion, this);
        }
    }

    private bool IntentarObtenerSiguiente(out EventoMensajeriaEntrada eventoMensajeria)
    {
        lock (sincronizacion)
        {
            if (eventos.Count == 0)
            {
                cerrado = true;
                eventoMensajeria = null!;
                return false;
            }

            eventoMensajeria = eventos.Dequeue();
            return true;
        }
    }

    private async Task ProcesarEventoConRenovacionAsync(
        EventoMensajeriaEntrada eventoMensajeria,
        CancellationToken cancellationToken)
    {
        EventoMensajeriaEntrada eventoActual = eventoMensajeria;

        while (true)
        {
            logger.LogInformation(
                "Inicia orquestacion de contexto. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                eventoActual.IDProcesamientoInternoMensaje,
                eventoActual.IDConversacion,
                eventoActual.IDLineaConversacion);

            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            IOrquestarMensajeContextoAplicacion orquestarMensajeContextoAplicacion = alcance.ServiceProvider
                .GetRequiredService<IOrquestarMensajeContextoAplicacion>();
            IRenovarLineaContextoAplicacion renovarLineaContextoAplicacion = alcance.ServiceProvider
                .GetRequiredService<IRenovarLineaContextoAplicacion>();

            ResultadoOrquestarMensajeContexto resultado = await orquestarMensajeContextoAplicacion.EjecutarAsync(
                eventoActual.IDProcesamientoInternoMensaje,
                cancellationToken);

            if (resultado.Tipo != ResultadoOrquestarMensajeContextoTipo.RenovarLinea)
            {
                if (resultado.Tipo == ResultadoOrquestarMensajeContextoTipo.Error)
                {
                    logger.LogWarning(
                        "Finaliza orquestacion de contexto con error controlado. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}, Error={Error}",
                        eventoActual.IDProcesamientoInternoMensaje,
                        eventoActual.IDConversacion,
                        resultado.Error);
                }
                else
                {
                    logger.LogInformation(
                        "Finaliza orquestacion de contexto. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
                        eventoActual.IDProcesamientoInternoMensaje,
                        eventoActual.IDConversacion);
                }

                return;
            }

            ResultadoRenovarLineaContexto resultadoRenovacion = await renovarLineaContextoAplicacion.EjecutarAsync(
                new SolicitudRenovarLineaContexto
                {
                    IDProcesamientoInternoMensaje = eventoActual.IDProcesamientoInternoMensaje,
                    IDMensaje = resultado.IDMensaje,
                    IDConversacion = resultado.IDConversacion,
                    IDLineaConversacionOrigen = resultado.IDLineaConversacion,
                    Compactacion = resultado.Compactacion
                        ?? throw new InvalidOperationException("La renovacion de linea requiere una compactacion.")
                },
                cancellationToken);

            eventoActual = new EventoMensajeriaEntrada
            {
                IDMensaje = resultadoRenovacion.IDMensaje,
                IDProcesamientoInternoMensaje = resultadoRenovacion.IDProcesamientoInternoMensaje,
                IDConversacion = resultadoRenovacion.IDConversacion,
                IDLineaConversacion = resultadoRenovacion.IDLineaConversacion,
                FechaCreacion = eventoActual.FechaCreacion
            };

            logger.LogInformation(
                "Linea de contexto renovada; el mismo mensaje se reintentara antes del siguiente. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                eventoActual.IDProcesamientoInternoMensaje,
                eventoActual.IDConversacion,
                eventoActual.IDLineaConversacion);
        }
    }

    private void Cerrar()
    {
        lock (sincronizacion)
        {
            cerrado = true;
            eventos.Clear();
            idsProcesamientos.Clear();
        }
    }
}
