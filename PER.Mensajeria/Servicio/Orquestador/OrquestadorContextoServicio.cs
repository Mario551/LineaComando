using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

namespace PER.Mensajeria.Servicio.Orquestador;

public sealed class OrquestadorContextoServicio : IOrquestadorContextoServicio
{
    private readonly ConcurrentDictionary<long, ProcesadorConversacionServicio> procesadores = new();
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<OrquestadorContextoServicio> logger;
    private readonly SemaphoreSlim limiteConversaciones;
    private readonly CancellationTokenSource cancelacion = new();
    private readonly object cicloVida = new();
    private Task? tareaDisposicion;
    private bool disponiendo;

    public OrquestadorContextoServicio(
        IServiceScopeFactory serviceScopeFactory,
        ConfiguracionOrquestadorContexto configuracion,
        ILogger<OrquestadorContextoServicio> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentNullException.ThrowIfNull(logger);

        if (configuracion.MaximoConversacionesConcurrentes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuracion),
                configuracion.MaximoConversacionesConcurrentes,
                "El maximo de conversaciones concurrentes debe ser mayor que cero.");
        }

        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
        limiteConversaciones = new SemaphoreSlim(
            configuracion.MaximoConversacionesConcurrentes,
            configuracion.MaximoConversacionesConcurrentes);
    }

    public Task EncolarAsync(EventoMensajeriaEntrada eventoMensajeria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventoMensajeria);
        cancellationToken.ThrowIfCancellationRequested();

        lock (cicloVida)
        {
            ObjectDisposedException.ThrowIf(disponiendo, this);

            while (true)
            {
                ProcesadorConversacionServicio procesador = procesadores.GetOrAdd(
                    eventoMensajeria.IDConversacion,
                    CrearProcesador);

                if (procesador.IntentarEncolar(eventoMensajeria))
                {
                    logger.LogDebug(
                        "Evento entregado al procesador de conversacion. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDConversacion={IDConversacion}",
                        eventoMensajeria.IDProcesamientoInternoMensaje,
                        eventoMensajeria.IDConversacion);
                    return Task.CompletedTask;
                }

                RetirarProcesador(eventoMensajeria.IDConversacion, procesador);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (cicloVida)
        {
            if (tareaDisposicion is null)
            {
                disponiendo = true;
                ProcesadorConversacionServicio[] procesadoresActivos = procesadores.Values
                    .Distinct()
                    .ToArray();
                tareaDisposicion = DisponerAsync(procesadoresActivos);
            }

            return new ValueTask(tareaDisposicion);
        }
    }

    private ProcesadorConversacionServicio CrearProcesador(long idConversacion)
    {
        logger.LogDebug(
            "Procesador de conversacion creado. IDConversacion={IDConversacion}",
            idConversacion);

        return new ProcesadorConversacionServicio(
            idConversacion,
            serviceScopeFactory,
            limiteConversaciones,
            RetirarProcesador,
            cancelacion.Token,
            logger);
    }

    private void RetirarProcesador(
        long idConversacion,
        ProcesadorConversacionServicio procesador)
    {
        bool retirado = ((ICollection<KeyValuePair<long, ProcesadorConversacionServicio>>)procesadores)
            .Remove(new KeyValuePair<long, ProcesadorConversacionServicio>(idConversacion, procesador));

        if (retirado)
        {
            logger.LogDebug(
                "Procesador de conversacion retirado. IDConversacion={IDConversacion}",
                idConversacion);
        }
    }

    private async Task DisponerAsync(IReadOnlyCollection<ProcesadorConversacionServicio> procesadoresActivos)
    {
        await Task.Yield();

        try
        {
            try
            {
                cancelacion.Cancel();
            }
            catch (Exception excepcion)
            {
                logger.LogError(excepcion, "Error notificando la cancelacion de los procesadores de conversacion.");
            }

            await Task.WhenAll(procesadoresActivos.Select(procesador => procesador.Finalizacion));
        }
        finally
        {
            procesadores.Clear();
            limiteConversaciones.Dispose();
            cancelacion.Dispose();
        }
    }
}
