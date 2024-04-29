namespace PER.Comandos.LineaComandos.Configuracion
{
    public interface ICreadorConfiguracion
    {
        event EventHandler<ArgsEventoConfiguracionCargada>? EventoConfiguracionCargada;

        IConfiguracion CopiaConfiguracion();
        Task RecargarAsync(CancellationToken token = default);
    }
}
