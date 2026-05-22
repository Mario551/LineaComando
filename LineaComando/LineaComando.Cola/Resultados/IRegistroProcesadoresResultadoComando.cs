namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public interface IRegistroProcesadoresResultadoComando
    {
        void Registrar(string rutaComando, IProcesadorResultadoComando procesador);

        IProcesadorResultadoComando? ObtenerPorRutaComando(string rutaComando);

        IProcesadorResultadoComando? ObtenerPorTipoVersion(string tipo, int version);
    }
}
