using System.Globalization;
using System.Text.Json;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Mensajeria.Aplicacion.Contexto;

namespace PER.Mensajeria.Builder.Contexto.EjecutorComandos.LineaComando;

public class EjecutorComandoLineaComandoServicio : IEjecutorComandoContextoServicio
{
    private readonly IColaComandosMemoria colaComandosMemoria;
    private readonly IAlmacenColaComandos almacenColaComandos;
    private readonly IResultadosComandos resultadosComandos;
    private readonly IRegistroProcesadoresSerializacionResultadosComando
        registroProcesadoresSerializacionResultadosComando;

    public EjecutorComandoLineaComandoServicio(
        IColaComandosMemoria colaComandosMemoria,
        IAlmacenColaComandos almacenColaComandos,
        IResultadosComandos resultadosComandos,
        IRegistroProcesadoresSerializacionResultadosComando
            registroProcesadoresSerializacionResultadosComando)
    {
        this.colaComandosMemoria = colaComandosMemoria;
        this.almacenColaComandos = almacenColaComandos;
        this.resultadosComandos = resultadosComandos;
        this.registroProcesadoresSerializacionResultadosComando =
            registroProcesadoresSerializacionResultadosComando;
    }

    public string Proveedor => "lineacomando";

    public async Task<ReferenciaEjecucionComandoContexto> EncolarAsync(
        SolicitudEjecutarComandoContexto solicitud,
        CancellationToken cancellationToken)
    {
        if (registroProcesadoresSerializacionResultadosComando
                .ObtenerPorRutaComando(solicitud.Comando.Codigo) is null)
        {
            throw new InvalidOperationException(
                $"El comando '{solicitud.Comando.Codigo}' debe registrar " +
                "IProcesadorResultadoComando mediante Resultado(...).");
        }

        ComandoEncolado comando = await colaComandosMemoria.EncolarAsync(
            new SolicitudComando
            {
                RutaComando = solicitud.Comando.Codigo,
                Argumentos = string.Empty,
                DatosDeComando = JsonSerializer.Serialize(solicitud.Parametros)
            },
            cancellationToken);

        return new ReferenciaEjecucionComandoContexto
        {
            Proveedor = Proveedor,
            IdentificadorExterno = comando.ComandoId.ToString(CultureInfo.InvariantCulture)
        };
    }

    public async Task<ConsultaEjecucionComandoContexto> ConsultarAsync(
        ReferenciaEjecucionComandoContexto referencia,
        CancellationToken cancellationToken)
    {
        long comandoId = ObtenerComandoId(referencia);
        ResultadoComandoPersistido? resultado = await almacenColaComandos.ObtenerResultadoPersistidoAsync(
            comandoId,
            cancellationToken);
        if (resultado is null)
        {
            return new ConsultaEjecucionComandoContexto
            {
                Estado = EstadoEjecucionComandoExternaContextoTipo.Inexistente,
                Error = $"El comando externo {comandoId} no existe."
            };
        }

        return new ConsultaEjecucionComandoContexto
        {
            Estado = resultado.Estado switch
            {
                "pendiente" => EstadoEjecucionComandoExternaContextoTipo.Pendiente,
                "procesando" => EstadoEjecucionComandoExternaContextoTipo.Procesando,
                "completado" => EstadoEjecucionComandoExternaContextoTipo.Completado,
                "fallido" => EstadoEjecucionComandoExternaContextoTipo.Fallido,
                _ => throw new InvalidOperationException(
                    $"El estado externo '{resultado.Estado}' del comando {comandoId} no es valido.")
            },
            Error = resultado.MensajeError
        };
    }

    public async Task<ResultadoComandoContexto> EsperarResultadoAsync(
        ReferenciaEjecucionComandoContexto referencia,
        CancellationToken cancellationToken)
    {
        long comandoId = ObtenerComandoId(referencia);
        ResultadoComandoPersistido? persistido = await almacenColaComandos.ObtenerResultadoPersistidoAsync(
            comandoId,
            cancellationToken);
        if (persistido is null)
        {
            return ResultadoComandoContexto.Fallo($"El comando externo {comandoId} no existe.");
        }

        if (persistido.Estado == "completado" && persistido.PayloadResultado is null)
        {
            return ResultadoComandoContexto.Fallo(
                $"El comando {comandoId} esta completado, pero no tiene payload durable recuperable.");
        }

        ResultadoComando? resultado;
        if (persistido.Estado is "completado" or "fallido")
        {
            resultado = await resultadosComandos.ObtenerResultadoAsync(comandoId, cancellationToken);
        }
        else
        {
            ComandoEncolado comando = await colaComandosMemoria.EsperarComandoAsync(comandoId, cancellationToken);
            resultado = await comando.Resultado.WaitAsync(cancellationToken);
        }

        if (resultado is null)
        {
            return ResultadoComandoContexto.Fallo(
                $"No fue posible recuperar el resultado durable del comando {comandoId}.");
        }

        if (!resultado.Exitoso)
        {
            return ResultadoComandoContexto.Fallo(
                resultado.MensajeError ?? $"El comando {comandoId} fallo sin detalle.");
        }

        string contenido = resultado.Salida is string salidaTexto
            ? salidaTexto
            : JsonSerializer.Serialize(resultado.Salida);
        return ResultadoComandoContexto.Exito(contenido);
    }

    public async Task AbandonarAsync(
        ReferenciaEjecucionComandoContexto referencia,
        string motivo,
        CancellationToken cancellationToken)
    {
        long comandoId = ObtenerComandoId(referencia);
        ResultadoComandoPersistido? resultado = await almacenColaComandos.ObtenerResultadoPersistidoAsync(
            comandoId,
            cancellationToken);
        if (resultado is null || resultado.Estado is "completado" or "fallido")
        {
            return;
        }

        await almacenColaComandos.MarcarComoProcesadoAsync(
            comandoId,
            ResultadoComando.Fallo(motivo),
            cancellationToken);
    }

    private long ObtenerComandoId(ReferenciaEjecucionComandoContexto referencia)
    {
        if (!string.Equals(referencia.Proveedor, Proveedor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La referencia pertenece al proveedor '{referencia.Proveedor}' y no a '{Proveedor}'.");
        }

        if (!long.TryParse(
                referencia.IdentificadorExterno,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long comandoId)
            || comandoId <= 0)
        {
            throw new InvalidOperationException(
                $"El identificador externo '{referencia.IdentificadorExterno}' no es un ComandoId valido.");
        }

        return comandoId;
    }
}
