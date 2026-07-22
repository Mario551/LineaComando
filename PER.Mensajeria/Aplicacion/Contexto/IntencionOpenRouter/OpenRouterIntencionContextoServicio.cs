using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public class OpenRouterIntencionContextoServicio : IIntencionContextoConversacionServicio
{
    private readonly IOpenRouterCliente cliente;
    private readonly IOpenRouterModeloAdaptador adaptador;
    private readonly ILogger<OpenRouterIntencionContextoServicio> logger;

    public OpenRouterIntencionContextoServicio(
        IOpenRouterCliente cliente,
        IOpenRouterModeloAdaptador adaptador,
        ILogger<OpenRouterIntencionContextoServicio> logger)
    {
        this.cliente = cliente;
        this.adaptador = adaptador;
        this.logger = logger;
    }

    public async Task<ResultadoIntencionContexto> DecidirAsync(
        SolicitudIntencionContexto solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        try
        {
            ResultadoOpenRouterCliente resultado = await cliente.CompletarChatAsync(
                adaptador.CrearSolicitudDecision(solicitud),
                cancellationToken);
            return adaptador.InterpretarDecision(solicitud, resultado);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Fallo la construccion o interpretacion de la decision OpenRouter. Iteracion={Iteracion}",
                solicitud.Iteracion);
            InformacionTecnicaLlamadaIAContexto metadata = adaptador.CrearInformacionTecnicaError(
                solicitud.Iteracion,
                "Error",
                excepcion.Message);
            string contenido = JsonSerializer.Serialize(new { accion = "error", error = excepcion.Message });
            return ResultadoIntencionContexto.ConError(metadata, contenido, excepcion.Message);
        }
    }

    public async Task<ResultadoCompactacionIntencionContexto> CompactarAsync(
        SolicitudCompactacionIntencionContexto solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        List<InformacionTecnicaLlamadaIAContexto> informacionesTecnicasLlamadasIA = [];
        try
        {
            List<string> fragmentos = CrearFragmentos(solicitud);
            if (fragmentos.Count == 0)
            {
                InformacionTecnicaLlamadaIAContexto metadata = adaptador.CrearInformacionTecnicaError(
                    solicitud.Iteracion,
                    "Compactar",
                    "No existe contenido para compactar.");
                return ResultadoCompactacionIntencionContexto.Fallo(
                    "No existe contenido para compactar.",
                    metadata);
            }

            int llamadas = 0;
            ResultadoCompactacionRecursiva resultado = await CompactarRecursivamenteAsync(
                solicitud,
                fragmentos,
                informacionesTecnicasLlamadasIA,
                () => llamadas++,
                () => llamadas,
                cancellationToken);

            if (resultado.Exitoso)
            {
                return ResultadoCompactacionIntencionContexto.Exito(resultado.Contenido!, informacionesTecnicasLlamadasIA);
            }

            return ResultadoCompactacionIntencionContexto.Fallo(
                resultado.Error ?? "No se pudo compactar el contexto.",
                informacionesTecnicasLlamadasIA);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Fallo la construccion o interpretacion de la compactacion OpenRouter. Iteracion={Iteracion}",
                solicitud.Iteracion);
            if (informacionesTecnicasLlamadasIA.Count == 0)
            {
                informacionesTecnicasLlamadasIA.Add(adaptador.CrearInformacionTecnicaError(
                    solicitud.Iteracion,
                    "Compactar",
                    excepcion.Message));
            }

            return ResultadoCompactacionIntencionContexto.Fallo(excepcion.Message, informacionesTecnicasLlamadasIA);
        }
    }

    private async Task<ResultadoCompactacionRecursiva> CompactarRecursivamenteAsync(
        SolicitudCompactacionIntencionContexto solicitud,
        IReadOnlyList<string> fragmentos,
        List<InformacionTecnicaLlamadaIAContexto> informacionesTecnicasLlamadasIA,
        Action registrarLlamada,
        Func<int> obtenerLlamadas,
        CancellationToken cancellationToken)
    {
        if (obtenerLlamadas() >= adaptador.MaximoLlamadasCompactacion)
        {
            return ResultadoCompactacionRecursiva.Fallo(
                "Se alcanzo el maximo de llamadas permitido para compactar el contexto.");
        }

        registrarLlamada();
        ResultadoOpenRouterCliente respuesta = await cliente.CompletarChatAsync(
            adaptador.CrearSolicitudCompactacion(solicitud, fragmentos),
            cancellationToken);
        ResultadoCompactacionOpenRouter interpretacion = adaptador.InterpretarCompactacion(solicitud, respuesta);
        informacionesTecnicasLlamadasIA.Add(interpretacion.InformacionTecnicaLlamadaIA);

        if (interpretacion.Exitoso)
        {
            return ResultadoCompactacionRecursiva.Exito(interpretacion.Contenido!);
        }

        if (!interpretacion.LimiteVentana)
        {
            return ResultadoCompactacionRecursiva.Fallo(
                interpretacion.Error ?? "OpenRouter no pudo compactar el contexto.");
        }

        if (fragmentos.Count == 1)
        {
            return ResultadoCompactacionRecursiva.Fallo(
                "Una entrada individual excede la ventana y no puede compactarse de forma segura.");
        }

        int puntoMedio = fragmentos.Count / 2;
        IReadOnlyList<string> primeraMitad = fragmentos.Take(puntoMedio).ToList();
        IReadOnlyList<string> segundaMitad = fragmentos.Skip(puntoMedio).ToList();

        ResultadoCompactacionRecursiva primera = await CompactarRecursivamenteAsync(
            solicitud,
            primeraMitad,
            informacionesTecnicasLlamadasIA,
            registrarLlamada,
            obtenerLlamadas,
            cancellationToken);
        if (!primera.Exitoso)
        {
            return primera;
        }

        ResultadoCompactacionRecursiva segunda = await CompactarRecursivamenteAsync(
            solicitud,
            segundaMitad,
            informacionesTecnicasLlamadasIA,
            registrarLlamada,
            obtenerLlamadas,
            cancellationToken);
        if (!segunda.Exitoso)
        {
            return segunda;
        }

        return await CompactarRecursivamenteAsync(
            solicitud,
            [primera.Contenido!, segunda.Contenido!],
            informacionesTecnicasLlamadasIA,
            registrarLlamada,
            obtenerLlamadas,
            cancellationToken);
    }

    private static List<string> CrearFragmentos(SolicitudCompactacionIntencionContexto solicitud)
    {
        List<string> fragmentos = [];
        if (solicitud.CompactacionContextoInicial is not null)
        {
            fragmentos.Add(JsonSerializer.Serialize(new
            {
                tipo = "compactacion_contexto_inicial",
                fecha = solicitud.CompactacionContextoInicial.FechaCreacion,
                contenido = solicitud.CompactacionContextoInicial.Contenido
            }));
        }

        fragmentos.AddRange(solicitud.MetadataEntradasContextoIA
            .Select(entrada => JsonSerializer.Serialize(new
            {
                tipo = "entrada_contexto_ia",
                entrada.Orden,
                rol = entrada.IDRolContextoIA,
                tipoEntrada = entrada.IDTipoEntradaContextoIA,
                entrada.Contenido,
                entrada.ToolCallID,
                fecha = entrada.FechaEntrada,
                reasoning = entrada.InformacionTecnicaLlamadaIA?.Reasoning,
                reasoningDetails = entrada.InformacionTecnicaLlamadaIA?.ReasoningDetailsJson
            })));

        return fragmentos;
    }

    private sealed class ResultadoCompactacionRecursiva
    {
        private ResultadoCompactacionRecursiva()
        {
        }

        public bool Exitoso { get; private set; }
        public string? Contenido { get; private set; }
        public string? Error { get; private set; }

        public static ResultadoCompactacionRecursiva Exito(string contenido)
        {
            return new ResultadoCompactacionRecursiva
            {
                Exitoso = true,
                Contenido = contenido
            };
        }

        public static ResultadoCompactacionRecursiva Fallo(string error)
        {
            return new ResultadoCompactacionRecursiva
            {
                Exitoso = false,
                Error = error
            };
        }
    }
}
