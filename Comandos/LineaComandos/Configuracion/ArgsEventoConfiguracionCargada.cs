namespace PER.Comandos.LineaComandos.Configuracion
{
    public class ArgsEventoConfiguracionCargada : EventArgs
    {
        public IConfiguracion Configuracion { get; private set; }

        public ArgsEventoConfiguracionCargada(IConfiguracion configuracion)
        {
            Configuracion = configuracion;
        }
    }
}
