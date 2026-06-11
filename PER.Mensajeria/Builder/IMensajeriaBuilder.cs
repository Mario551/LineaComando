using PER.Mensajeria.Aplicacion.Contexto;

namespace PER.Mensajeria.Builder;

public interface IMensajeriaBuilder
{
    IMensajeriaBuilder UsarPostgreSQL(string cadenaConexion);
    IMensajeriaBuilder UsarPostgreSQL(string cadenaConexion, string? esquema);
    IMensajeriaBuilder UsarSqlServer(string cadenaConexion);
    IMensajeriaBuilder UsarSqlServer(string cadenaConexion, string? esquema);
    IMensajeriaBuilder ConfigurarLineaConversacion(TimeSpan tiempoMaximoInactividad);
    IMensajeriaBuilder ConfigurarContexto(Action<IContextoMensajeriaBuilder> configurarContexto);
    IMensajeriaBuilder ConfigurarContextoConversacion(ConfiguracionContextoConversacion configuracion);
    IMensajeriaBuilder AgregarWorkerOrquestador();
}
