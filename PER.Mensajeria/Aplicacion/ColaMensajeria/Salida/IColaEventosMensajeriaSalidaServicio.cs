namespace PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;

public interface IColaEventosMensajeriaSalidaServicio
{
    void Publicar(EventoMensajeriaSalida eventoMensajeria);
    void PublicarRehidratado(EventoMensajeriaSalida eventoMensajeria);
    Task<EventoMensajeriaSalida> ConsumirAsync(CancellationToken cancellationToken);
}
