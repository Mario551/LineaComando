using PER.Comandos.Tipos.Resultado;

namespace PER.Comandos.LineaComandos.Configuracion
{
    public interface IConfiguracion : ICloneable
    {
        Resultado<T> Get<T>(string nombre);
    }
}
