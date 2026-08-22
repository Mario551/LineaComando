namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public interface IRegistroProcesadoresSerializacionResultadosComando
    {
        void Registrar(string rutaComando, IProcesadorResultadoComando procesador);

        IProcesadorResultadoComando? ObtenerPorRutaComando(string rutaComando);

        IProcesadorResultadoComando? ObtenerPorTipoVersion(string tipo, int version);
    }
}
