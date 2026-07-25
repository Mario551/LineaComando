using PER.Mensajeria.Aplicacion.Contexto;

namespace PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;

public sealed class ResultadoOrquestarMensajeContexto
{
    private ResultadoOrquestarMensajeContexto()
    {
    }

    public ResultadoOrquestarMensajeContextoTipo Tipo { get; private set; }
    public ResultadoCompactacionIntencionContexto? Compactacion { get; private set; }
    public long IDMensaje { get; private set; }
    public IReadOnlyList<long> IDsMensajes { get; private set; } = [];
    public IReadOnlyList<long> IDsProcesamientosInternosMensaje { get; private set; } = [];
    public long IDConversacion { get; private set; }
    public long IDLineaConversacion { get; private set; }
    public string? Error { get; private set; }

    public static ResultadoOrquestarMensajeContexto Procesado()
    {
        return new ResultadoOrquestarMensajeContexto
        {
            Tipo = ResultadoOrquestarMensajeContextoTipo.Procesado
        };
    }

    public static ResultadoOrquestarMensajeContexto SinSalidas()
    {
        return new ResultadoOrquestarMensajeContexto
        {
            Tipo = ResultadoOrquestarMensajeContextoTipo.SinSalidas
        };
    }

    public static ResultadoOrquestarMensajeContexto RenovarLinea(
        ResultadoCompactacionIntencionContexto compactacion,
        long idMensaje,
        IReadOnlyList<long> idsMensajes,
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        long idConversacion,
        long idLineaConversacion)
    {
        ArgumentNullException.ThrowIfNull(compactacion);

        return new ResultadoOrquestarMensajeContexto
        {
            Tipo = ResultadoOrquestarMensajeContextoTipo.RenovarLinea,
            Compactacion = compactacion,
            IDMensaje = idMensaje,
            IDsMensajes = idsMensajes,
            IDsProcesamientosInternosMensaje = idsProcesamientosInternosMensaje,
            IDConversacion = idConversacion,
            IDLineaConversacion = idLineaConversacion
        };
    }

    public static ResultadoOrquestarMensajeContexto ConError(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ResultadoOrquestarMensajeContexto
        {
            Tipo = ResultadoOrquestarMensajeContextoTipo.Error,
            Error = error
        };
    }
}
