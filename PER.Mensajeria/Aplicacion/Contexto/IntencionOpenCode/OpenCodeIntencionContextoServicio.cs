using System.Text.Json;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class OpenCodeIntencionContextoServicio
    : IIntencionContextoConversacionServicio
{
    private const int MaximoLlamadasCompactacion = 32;
    private static readonly TimeSpan TiempoLimpiezaSesion =
        TimeSpan.FromSeconds(10);

    private readonly IOpenCodeCliente cliente;
    private readonly IOpenCodeAgenteAdaptador adaptador;
    private readonly ILogger<OpenCodeIntencionContextoServicio> logger;

    public OpenCodeIntencionContextoServicio(
        IOpenCodeCliente cliente,
        IOpenCodeAgenteAdaptador adaptador,
        ILogger<OpenCodeIntencionContextoServicio> logger)
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
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado =
                await EjecutarEnSesionAsync(
                    $"PER.Mensajeria decision {solicitud.Solicitud.IDProcesamientoInternoMensaje} iteracion {solicitud.Iteracion}",
                    adaptador.CrearSolicitudDecision(solicitud),
                    cancellationToken);

            return adaptador.InterpretarDecision(
                solicitud,
                resultado);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Fallo la decision OpenCode. Iteracion={Iteracion}",
                solicitud.Iteracion);
            InformacionTecnicaLlamadaIAContexto informacionTecnica =
                adaptador.CrearInformacionTecnicaError(
                    solicitud.Iteracion,
                    "Error",
                    excepcion.Message);
            string contenido = JsonSerializer.Serialize(
                new
                {
                    accion = "error",
                    error = excepcion.Message
                });
            return ResultadoIntencionContexto.ConError(
                informacionTecnica,
                contenido,
                excepcion.Message);
        }
    }

    public async Task<ResultadoCompactacionIntencionContexto> CompactarAsync(
        SolicitudCompactacionIntencionContexto solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        List<InformacionTecnicaLlamadaIAContexto>
            informacionesTecnicasLlamadasIA = [];
        try
        {
            List<string> fragmentos = CrearFragmentos(solicitud);
            if (fragmentos.Count == 0)
            {
                InformacionTecnicaLlamadaIAContexto informacionTecnica =
                    adaptador.CrearInformacionTecnicaError(
                        solicitud.Iteracion,
                        "Compactar",
                        "No existe contenido para compactar.");
                return ResultadoCompactacionIntencionContexto.Fallo(
                    "No existe contenido para compactar.",
                    informacionTecnica);
            }

            ContadorLlamadas contador = new();
            ResultadoCompactacionRecursiva resultado =
                await CompactarRecursivamenteAsync(
                    solicitud,
                    fragmentos,
                    informacionesTecnicasLlamadasIA,
                    contador,
                    cancellationToken);

            if (resultado.Exitoso)
            {
                return ResultadoCompactacionIntencionContexto.Exito(
                    resultado.Contenido!,
                    informacionesTecnicasLlamadasIA);
            }

            return ResultadoCompactacionIntencionContexto.Fallo(
                resultado.Error
                    ?? "No se pudo compactar el contexto con OpenCode.",
                informacionesTecnicasLlamadasIA);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Fallo la compactacion OpenCode. Iteracion={Iteracion}",
                solicitud.Iteracion);
            if (informacionesTecnicasLlamadasIA.Count == 0)
            {
                informacionesTecnicasLlamadasIA.Add(
                    adaptador.CrearInformacionTecnicaError(
                        solicitud.Iteracion,
                        "Compactar",
                        excepcion.Message));
            }

            return ResultadoCompactacionIntencionContexto.Fallo(
                excepcion.Message,
                informacionesTecnicasLlamadasIA);
        }
    }

    private async Task<ResultadoCompactacionRecursiva>
        CompactarRecursivamenteAsync(
            SolicitudCompactacionIntencionContexto solicitud,
            IReadOnlyList<string> fragmentos,
            List<InformacionTecnicaLlamadaIAContexto>
                informacionesTecnicasLlamadasIA,
            ContadorLlamadas contador,
            CancellationToken cancellationToken)
    {
        if (contador.Total >= MaximoLlamadasCompactacion)
        {
            return ResultadoCompactacionRecursiva.Fallo(
                "Se alcanzo el maximo de llamadas permitido para compactar el contexto.");
        }

        contador.Total++;
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> respuesta =
            await EjecutarEnSesionAsync(
                $"PER.Mensajeria compactacion {solicitud.Solicitud.IDConversacion} llamada {contador.Total}",
                adaptador.CrearSolicitudCompactacion(
                    solicitud,
                    fragmentos),
                cancellationToken);
        ResultadoCompactacionOpenCode interpretacion =
            adaptador.InterpretarCompactacion(
                solicitud,
                respuesta);
        informacionesTecnicasLlamadasIA.Add(
            interpretacion.InformacionTecnicaLlamadaIA);

        if (interpretacion.Exitoso)
        {
            return ResultadoCompactacionRecursiva.Exito(
                interpretacion.Contenido!);
        }

        if (!interpretacion.LimiteVentana)
        {
            return ResultadoCompactacionRecursiva.Fallo(
                interpretacion.Error
                    ?? "OpenCode no pudo compactar el contexto.");
        }

        if (fragmentos.Count == 1)
        {
            return ResultadoCompactacionRecursiva.Fallo(
                "Una entrada individual excede la ventana y no puede compactarse de forma segura.");
        }

        int puntoMedio = fragmentos.Count / 2;
        IReadOnlyList<string> primeraMitad =
            fragmentos.Take(puntoMedio).ToList();
        IReadOnlyList<string> segundaMitad =
            fragmentos.Skip(puntoMedio).ToList();

        ResultadoCompactacionRecursiva primera =
            await CompactarRecursivamenteAsync(
                solicitud,
                primeraMitad,
                informacionesTecnicasLlamadasIA,
                contador,
                cancellationToken);
        if (!primera.Exitoso)
        {
            return primera;
        }

        ResultadoCompactacionRecursiva segunda =
            await CompactarRecursivamenteAsync(
                solicitud,
                segundaMitad,
                informacionesTecnicasLlamadasIA,
                contador,
                cancellationToken);
        if (!segunda.Exitoso)
        {
            return segunda;
        }

        return await CompactarRecursivamenteAsync(
            solicitud,
            [primera.Contenido!, segunda.Contenido!],
            informacionesTecnicasLlamadasIA,
            contador,
            cancellationToken);
    }

    private async Task<ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>>
        EjecutarEnSesionAsync(
            string titulo,
            DTOOpenCodeMensajeSolicitud solicitud,
            CancellationToken cancellationToken)
    {
        ResultadoOpenCodeCliente<DTOOpenCodeSesion> resultadoSesion =
            await cliente.CrearSesionAsync(
                new DTOOpenCodeCrearSesionSolicitud
                {
                    Titulo = titulo
                },
                cancellationToken);

        if (!resultadoSesion.Exitoso
            || string.IsNullOrWhiteSpace(resultadoSesion.Respuesta?.ID))
        {
            string error = resultadoSesion.Error
                ?? "OpenCode no devolvio el identificador de la sesion.";
            return ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>.Fallo(
                resultadoSesion.CodigoEstado,
                resultadoSesion.SolicitudJson,
                resultadoSesion.RespuestaJson,
                error,
                resultadoSesion.TipoError,
                resultadoSesion.ErrorOpenCode);
        }

        string idSesion = resultadoSesion.Respuesta.ID;
        bool abortar = false;
        try
        {
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado =
                await cliente.EnviarMensajeAsync(
                    idSesion,
                    solicitud,
                    cancellationToken);
            abortar = !resultado.Exitoso;
            return resultado;
        }
        catch (OperationCanceledException)
        {
            abortar = true;
            throw;
        }
        catch
        {
            abortar = true;
            throw;
        }
        finally
        {
            await LimpiarSesionAsync(
                idSesion,
                abortar);
        }
    }

    private async Task LimpiarSesionAsync(
        string idSesion,
        bool abortar)
    {
        if (abortar)
        {
            await EjecutarLimpiezaAsync(
                token => cliente.AbortarSesionAsync(
                    idSesion,
                    token),
                idSesion,
                "abortar");
        }

        await EjecutarLimpiezaAsync(
            token => cliente.EliminarSesionAsync(
                idSesion,
                token),
            idSesion,
            "eliminar");
    }

    private async Task EjecutarLimpiezaAsync(
        Func<
            CancellationToken,
            Task<ResultadoOpenCodeCliente<bool>>> ejecutar,
        string idSesion,
        string operacion)
    {
        using CancellationTokenSource cancelacionLimpieza =
            new(TiempoLimpiezaSesion);
        try
        {
            ResultadoOpenCodeCliente<bool> resultado =
                await ejecutar(cancelacionLimpieza.Token);
            if (!resultado.Exitoso || resultado.Respuesta is not true)
            {
                logger.LogWarning(
                    "No se pudo {Operacion} la sesion OpenCode {IDSesion}. Error={Error}",
                    operacion,
                    idSesion,
                    resultado.Error);
            }
        }
        catch (Exception excepcion)
        {
            logger.LogWarning(
                excepcion,
                "No se pudo {Operacion} la sesion OpenCode {IDSesion}.",
                operacion,
                idSesion);
        }
    }

    private static List<string> CrearFragmentos(
        SolicitudCompactacionIntencionContexto solicitud)
    {
        List<string> fragmentos = [];
        if (solicitud.CompactacionContextoInicial is not null)
        {
            fragmentos.Add(JsonSerializer.Serialize(
                new
                {
                    tipo = "compactacion_contexto_inicial",
                    fecha =
                        solicitud.CompactacionContextoInicial.FechaCreacion,
                    contenido =
                        solicitud.CompactacionContextoInicial.Contenido
                }));
        }

        fragmentos.AddRange(
            solicitud.MetadataEntradasContextoIA
                .OrderBy(entrada => entrada.Orden)
                .ThenBy(entrada => entrada.ID)
                .Select(entrada => JsonSerializer.Serialize(
                    new
                    {
                        tipo = "metadata_entrada_contexto_ia",
                        entrada.Orden,
                        rol = entrada.IDRolContextoIA,
                        tipoEntrada =
                            entrada.IDTipoEntradaContextoIA,
                        entrada.Contenido,
                        entrada.ToolCallID,
                        fecha = entrada.FechaEntrada,
                        reasoning =
                            entrada.InformacionTecnicaLlamadaIA?.Reasoning,
                        reasoningDetails =
                            entrada.InformacionTecnicaLlamadaIA
                                ?.ReasoningDetailsJson
                    })));

        return fragmentos;
    }

    private sealed class ContadorLlamadas
    {
        public int Total { get; set; }
    }

    private sealed class ResultadoCompactacionRecursiva
    {
        private ResultadoCompactacionRecursiva()
        {
        }

        public bool Exitoso { get; private set; }
        public string? Contenido { get; private set; }
        public string? Error { get; private set; }

        public static ResultadoCompactacionRecursiva Exito(
            string contenido)
        {
            return new ResultadoCompactacionRecursiva
            {
                Exitoso = true,
                Contenido = contenido
            };
        }

        public static ResultadoCompactacionRecursiva Fallo(
            string error)
        {
            return new ResultadoCompactacionRecursiva
            {
                Exitoso = false,
                Error = error
            };
        }
    }
}
