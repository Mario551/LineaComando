using System.Text.Json;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;
using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace BuilderTest.Infraestructura;

public sealed class IntencionOpenCodeIntegracionPrueba
    : IIntencionContextoConversacionServicio
{
    private const int MaximoLlamadasCompactacion = 32;
    private static readonly TimeSpan TiempoLimpiezaSesion =
        TimeSpan.FromSeconds(10);

    private readonly IOpenCodeCliente cliente;
    private readonly IOpenCodeAgenteAdaptador adaptador;
    private readonly RegistroArtefactosOpenCodePrueba artefactos;
    private readonly ILogger<IntencionOpenCodeIntegracionPrueba> logger;

    public IntencionOpenCodeIntegracionPrueba(
        IOpenCodeCliente cliente,
        IOpenCodeAgenteAdaptador adaptador,
        RegistroArtefactosOpenCodePrueba artefactos,
        ILogger<IntencionOpenCodeIntegracionPrueba> logger)
    {
        this.cliente = cliente;
        this.adaptador = adaptador;
        this.artefactos = artefactos;
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
                    "decision",
                    solicitud.Iteracion,
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
                "Fallo la decision OpenCode de integracion. Iteracion={Iteracion}",
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
                "Fallo la compactacion OpenCode de integracion. Iteracion={Iteracion}",
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
                "compactacion",
                solicitud.Iteracion,
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
            string proposito,
            int iteracion,
            DTOOpenCodeMensajeSolicitud solicitud,
            CancellationToken cancellationToken)
    {
        DTOOpenCodeCrearSesionSolicitud solicitudSesion =
            new()
            {
                Titulo = titulo
            };
        DateTime inicioCreacion = DateTime.UtcNow;
        ResultadoOpenCodeCliente<DTOOpenCodeSesion> resultadoSesion;
        try
        {
            resultadoSesion = await cliente.CrearSesionAsync(
                solicitudSesion,
                cancellationToken);
            await artefactos.RegistrarLlamadaAsync(
                "crear_sesion",
                proposito,
                iteracion,
                resultadoSesion.Respuesta?.ID,
                inicioCreacion,
                DateTime.UtcNow,
                resultadoSesion);
        }
        catch (Exception excepcion)
        {
            await artefactos.RegistrarExcepcionAsync(
                "crear_sesion",
                proposito,
                iteracion,
                null,
                inicioCreacion,
                excepcion);
            throw;
        }

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
        DateTime inicioMensaje = DateTime.UtcNow;
        try
        {
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado =
                await cliente.EnviarMensajeAsync(
                    idSesion,
                    solicitud,
                    cancellationToken);
            await artefactos.RegistrarLlamadaAsync(
                "enviar_mensaje",
                proposito,
                iteracion,
                idSesion,
                inicioMensaje,
                DateTime.UtcNow,
                resultado);
            abortar = !resultado.Exitoso;
            return resultado;
        }
        catch (Exception excepcion)
        {
            abortar = true;
            await artefactos.RegistrarExcepcionAsync(
                "enviar_mensaje",
                proposito,
                iteracion,
                idSesion,
                inicioMensaje,
                excepcion);
            throw;
        }
        finally
        {
            await LimpiarSesionAsync(
                idSesion,
                proposito,
                iteracion,
                abortar);
        }
    }

    private async Task LimpiarSesionAsync(
        string idSesion,
        string proposito,
        int iteracion,
        bool abortar)
    {
        if (abortar)
        {
            await EjecutarLimpiezaAsync(
                token => cliente.AbortarSesionAsync(
                    idSesion,
                    token),
                idSesion,
                proposito,
                iteracion,
                "abortar_sesion");
        }

        await EjecutarLimpiezaAsync(
            token => cliente.EliminarSesionAsync(
                idSesion,
                token),
            idSesion,
            proposito,
            iteracion,
            "eliminar_sesion");
    }

    private async Task EjecutarLimpiezaAsync(
        Func<
            CancellationToken,
            Task<ResultadoOpenCodeCliente<bool>>> ejecutar,
        string idSesion,
        string proposito,
        int iteracion,
        string operacion)
    {
        using CancellationTokenSource cancelacionLimpieza =
            new(TiempoLimpiezaSesion);
        DateTime fechaInicio = DateTime.UtcNow;
        try
        {
            ResultadoOpenCodeCliente<bool> resultado =
                await ejecutar(cancelacionLimpieza.Token);
            await artefactos.RegistrarLlamadaAsync(
                operacion,
                proposito,
                iteracion,
                idSesion,
                fechaInicio,
                DateTime.UtcNow,
                resultado);
            if (!resultado.Exitoso || resultado.Respuesta is not true)
            {
                logger.LogWarning(
                    "No se pudo completar {Operacion} para la sesion OpenCode {IDSesion}. Error={Error}",
                    operacion,
                    idSesion,
                    resultado.Error);
            }
        }
        catch (Exception excepcion)
        {
            await artefactos.RegistrarExcepcionAsync(
                operacion,
                proposito,
                iteracion,
                idSesion,
                fechaInicio,
                excepcion);
            logger.LogWarning(
                excepcion,
                "No se pudo completar {Operacion} para la sesion OpenCode {IDSesion}.",
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
