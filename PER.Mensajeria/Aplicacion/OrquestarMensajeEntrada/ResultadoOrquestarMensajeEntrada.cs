using PER.Mensajeria.Aplicacion.Contexto;

namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

public sealed class ResultadoOrquestarMensajeEntrada
{
    private ResultadoOrquestarMensajeEntrada()
    {
    }

    public ResultadoOrquestarMensajeEntradaTipo Tipo { get; private set; }
    public ResultadoCompactacionIntencionContexto? Compactacion { get; private set; }
    public long IDMensaje { get; private set; }
    public long IDConversacion { get; private set; }
    public long IDLineaConversacion { get; private set; }
    public string? Error { get; private set; }

    public static ResultadoOrquestarMensajeEntrada Procesado()
    {
        return new ResultadoOrquestarMensajeEntrada
        {
            Tipo = ResultadoOrquestarMensajeEntradaTipo.Procesado
        };
    }

    public static ResultadoOrquestarMensajeEntrada SinSalidas()
    {
        return new ResultadoOrquestarMensajeEntrada
        {
            Tipo = ResultadoOrquestarMensajeEntradaTipo.SinSalidas
        };
    }

    public static ResultadoOrquestarMensajeEntrada RenovarLinea(
        ResultadoCompactacionIntencionContexto compactacion,
        long idMensaje,
        long idConversacion,
        long idLineaConversacion)
    {
        ArgumentNullException.ThrowIfNull(compactacion);

        return new ResultadoOrquestarMensajeEntrada
        {
            Tipo = ResultadoOrquestarMensajeEntradaTipo.RenovarLinea,
            Compactacion = compactacion,
            IDMensaje = idMensaje,
            IDConversacion = idConversacion,
            IDLineaConversacion = idLineaConversacion
        };
    }

    public static ResultadoOrquestarMensajeEntrada ConError(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ResultadoOrquestarMensajeEntrada
        {
            Tipo = ResultadoOrquestarMensajeEntradaTipo.Error,
            Error = error
        };
    }
}
