using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;

namespace PER.Mensajeria.Servicio.Orquestador;

internal sealed class ProcesadorConversacionServicio
{
    private const string EstadoPendiente = "pendiente";
    private const string EstadoEnProceso = "en_proceso";

    private readonly long idConversacion;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly SemaphoreSlim limiteConversaciones;
    private readonly ConfiguracionAgrupacionMensajesEntrada configuracionAgrupacion;
    private readonly Action<long, ProcesadorConversacionServicio> retirarProcesador;
    private readonly CancellationToken cancellationToken;
    private readonly ILogger<OrquestadorContextoServicio> logger;
    private readonly object sincronizacion = new();
    private readonly Queue<EventoMensajeriaEntrada> eventos = new();
    private readonly HashSet<long> idsProcesamientos = [];
    private TaskCompletionSource<bool> cambioEventos = CrearFuenteCambio();
    private long marcaUltimoEvento;
    private Task? tareaProcesamiento;
    private bool cerrado;

    public ProcesadorConversacionServicio(
        long idConversacion,
        IServiceScopeFactory serviceScopeFactory,
        SemaphoreSlim limiteConversaciones,
        ConfiguracionAgrupacionMensajesEntrada configuracionAgrupacion,
        Action<long, ProcesadorConversacionServicio> retirarProcesador,
        CancellationToken cancellationToken,
        ILogger<OrquestadorContextoServicio> logger)
    {
        this.idConversacion = idConversacion;
        this.serviceScopeFactory = serviceScopeFactory;
        this.limiteConversaciones = limiteConversaciones;
        this.configuracionAgrupacion = configuracionAgrupacion;
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
        if (eventoMensajeria.IDEstadoProcesamientoInternoMensaje
            is not EstadoPendiente and not EstadoEnProceso)
        {
            throw new InvalidOperationException(
                $"El evento {eventoMensajeria.IDProcesamientoInternoMensaje} tiene el estado no soportado '{eventoMensajeria.IDEstadoProcesamientoInternoMensaje}'.");
        }

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
            marcaUltimoEvento = Stopwatch.GetTimestamp();
            TaskCompletionSource<bool> cambioAnterior = cambioEventos;
            cambioEventos = CrearFuenteCambio();
            tareaProcesamiento ??= ProcesarColaAsync();
            cambioAnterior.TrySetResult(true);
            return true;
        }
    }

    private async Task ProcesarColaAsync()
    {
        try
        {
            await Task.Yield();

            while (true)
            {
                LoteEventosMensajeriaEntrada lote = await EsperarLoteAsync(cancellationToken);
                bool cupoAdquirido = false;

                try
                {
                    await limiteConversaciones.WaitAsync(cancellationToken);
                    cupoAdquirido = true;
                    await ProcesarLoteConRenovacionAsync(lote, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception excepcion)
                {
                    logger.LogError(
                        excepcion,
                        "Error aislado procesando lote de contexto. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}, IDConversacion={IDConversacion}",
                        string.Join(",", lote.Eventos.Select(evento => evento.IDProcesamientoInternoMensaje)),
                        lote.IDConversacion);
                }
                finally
                {
                    if (cupoAdquirido)
                    {
                        limiteConversaciones.Release();
                    }

                    lock (sincronizacion)
                    {
                        foreach (EventoMensajeriaEntrada evento in lote.Eventos)
                        {
                            idsProcesamientos.Remove(evento.IDProcesamientoInternoMensaje);
                        }
                    }
                }

                if (CerrarSiNoHayEventos())
                {
                    break;
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
            retirarProcesador(idConversacion, this);
        }
    }

    private async Task<LoteEventosMensajeriaEntrada> EsperarLoteAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            Task tareaCambio;
            TimeSpan tiempoEspera;
            lock (sincronizacion)
            {
                if (eventos.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"El procesador de la conversacion {idConversacion} no tiene eventos para agrupar.");
                }

                int cantidadPrimerGrupo = ContarEventosPrimerGrupo();
                EventoMensajeriaEntrada primerEvento = eventos.Peek();
                bool eventoRehidratadoEnProceso =
                    primerEvento.IDEstadoProcesamientoInternoMensaje == EstadoEnProceso;
                bool alcanzoLimite = cantidadPrimerGrupo >= configuracionAgrupacion.CantidadMaximaMensajesPorLote;
                bool cambioGrupo = cantidadPrimerGrupo < eventos.Count;

                if (eventoRehidratadoEnProceso || alcanzoLimite || cambioGrupo)
                {
                    return ExtraerLote(cantidadPrimerGrupo);
                }

                TimeSpan tiempoTranscurrido = Stopwatch.GetElapsedTime(marcaUltimoEvento);
                if (tiempoTranscurrido >= configuracionAgrupacion.TiempoInactividad)
                {
                    return ExtraerLote(cantidadPrimerGrupo);
                }

                tiempoEspera = configuracionAgrupacion.TiempoInactividad - tiempoTranscurrido;
                tareaCambio = cambioEventos.Task;
            }

            try
            {
                await tareaCambio.WaitAsync(tiempoEspera, cancellationToken);
                continue;
            }
            catch (TimeoutException)
            {
            }

            lock (sincronizacion)
            {
                if (!ReferenceEquals(tareaCambio, cambioEventos.Task))
                {
                    continue;
                }

                return ExtraerLote(ContarEventosPrimerGrupo());
            }
        }
    }

    private int ContarEventosPrimerGrupo()
    {
        EventoMensajeriaEntrada primerEvento = eventos.Peek();
        long idLineaConversacion = primerEvento.IDLineaConversacion;
        string estadoProcesamiento = primerEvento.IDEstadoProcesamientoInternoMensaje;
        bool limitarCantidad = estadoProcesamiento == EstadoPendiente;
        int cantidad = 0;

        foreach (EventoMensajeriaEntrada evento in eventos)
        {
            if (evento.IDLineaConversacion != idLineaConversacion
                || evento.IDEstadoProcesamientoInternoMensaje != estadoProcesamiento
                || limitarCantidad
                    && cantidad == configuracionAgrupacion.CantidadMaximaMensajesPorLote)
            {
                break;
            }

            cantidad++;
        }

        return cantidad;
    }

    private LoteEventosMensajeriaEntrada ExtraerLote(int cantidad)
    {
        List<EventoMensajeriaEntrada> eventosLote = new(cantidad);

        for (int indice = 0; indice < cantidad; indice++)
        {
            eventosLote.Add(eventos.Dequeue());
        }

        EventoMensajeriaEntrada primerEvento = eventosLote[0];
        return new LoteEventosMensajeriaEntrada
        {
            IDConversacion = primerEvento.IDConversacion,
            IDLineaConversacion = primerEvento.IDLineaConversacion,
            Eventos = eventosLote
        };
    }

    private bool CerrarSiNoHayEventos()
    {
        lock (sincronizacion)
        {
            if (eventos.Count > 0)
            {
                return false;
            }

            cerrado = true;
            return true;
        }
    }

    private async Task ProcesarLoteConRenovacionAsync(
        LoteEventosMensajeriaEntrada lote,
        CancellationToken cancellationToken)
    {
        LoteEventosMensajeriaEntrada loteActual = lote;

        while (true)
        {
            IReadOnlyList<long> idsProcesamientos = loteActual.Eventos
                .Select(evento => evento.IDProcesamientoInternoMensaje)
                .ToList();
            logger.LogInformation(
                "Inicia orquestacion de lote de contexto. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                string.Join(",", idsProcesamientos),
                loteActual.IDConversacion,
                loteActual.IDLineaConversacion);

            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            IOrquestarMensajeContextoAplicacion orquestarMensajeContextoAplicacion = alcance.ServiceProvider
                .GetRequiredService<IOrquestarMensajeContextoAplicacion>();
            IRenovarLineaContextoAplicacion renovarLineaContextoAplicacion = alcance.ServiceProvider
                .GetRequiredService<IRenovarLineaContextoAplicacion>();

            ResultadoOrquestarMensajeContexto resultado = await orquestarMensajeContextoAplicacion.EjecutarAsync(
                idsProcesamientos,
                cancellationToken);

            if (resultado.Tipo != ResultadoOrquestarMensajeContextoTipo.RenovarLinea)
            {
                if (resultado.Tipo == ResultadoOrquestarMensajeContextoTipo.Error)
                {
                    logger.LogWarning(
                        "Finaliza orquestacion de lote de contexto con error controlado. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}, IDConversacion={IDConversacion}, Error={Error}",
                        string.Join(",", idsProcesamientos),
                        loteActual.IDConversacion,
                        resultado.Error);
                }
                else
                {
                    logger.LogInformation(
                        "Finaliza orquestacion de lote de contexto. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}, IDConversacion={IDConversacion}",
                        string.Join(",", idsProcesamientos),
                        loteActual.IDConversacion);
                }

                return;
            }

            IReadOnlyList<long> idsProcesamientosRenovacion =
                resultado.IDsProcesamientosInternosMensaje;
            HashSet<long> idsProcesamientosRenovacionSet =
                idsProcesamientosRenovacion.ToHashSet();
            ResultadoRenovarLineaContexto resultadoRenovacion = await renovarLineaContextoAplicacion.EjecutarAsync(
                new SolicitudRenovarLineaContexto
                {
                    IDProcesamientoInternoMensaje = resultado.IDsProcesamientosInternosMensaje[0],
                    IDsProcesamientosInternosMensaje = idsProcesamientosRenovacion,
                    IDMensaje = resultado.IDMensaje,
                    IDsMensajes = resultado.IDsMensajes,
                    IDConversacion = resultado.IDConversacion,
                    IDLineaConversacionOrigen = resultado.IDLineaConversacion,
                    Compactacion = resultado.Compactacion
                        ?? throw new InvalidOperationException("La renovacion de linea requiere una compactacion.")
                },
                cancellationToken);

            loteActual = new LoteEventosMensajeriaEntrada
            {
                IDConversacion = resultadoRenovacion.IDConversacion,
                IDLineaConversacion = resultadoRenovacion.IDLineaConversacion,
                Eventos = loteActual.Eventos
                    .Where(evento =>
                        idsProcesamientosRenovacionSet.Contains(evento.IDProcesamientoInternoMensaje))
                    .Select(evento => new EventoMensajeriaEntrada
                    {
                        IDMensaje = evento.IDMensaje,
                        IDProcesamientoInternoMensaje = evento.IDProcesamientoInternoMensaje,
                        IDEstadoProcesamientoInternoMensaje = EstadoPendiente,
                        IDConversacion = resultadoRenovacion.IDConversacion,
                        IDLineaConversacion = resultadoRenovacion.IDLineaConversacion,
                        FechaCreacion = evento.FechaCreacion
                    })
                    .ToList()
            };

            logger.LogInformation(
                "Linea de contexto renovada; el mismo lote se reintentara antes del siguiente. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                string.Join(",", idsProcesamientos),
                loteActual.IDConversacion,
                loteActual.IDLineaConversacion);
        }
    }

    private static TaskCompletionSource<bool> CrearFuenteCambio()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
