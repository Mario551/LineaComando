using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.Tipos.Resultado;

namespace ComandosColaTest.Helpers
{
    /// <summary>
    /// Configuración simple para pruebas.
    /// </summary>
    public class ConfiguracionPrueba : IConfiguracion
    {
        private readonly Dictionary<string, object> _valores = new();

        public Resultado<T> Get<T>(string nombre)
        {
            if (_valores.TryGetValue(nombre, out var valor) && valor is T tipoValor)
            {
                return new Resultado<T>(true, tipoValor);
            }

            return new Resultado<T>(false);
        }

        public void Set(string nombre, object valor)
        {
            _valores[nombre] = valor;
        }

        public object Clone()
        {
            var clon = new ConfiguracionPrueba();
            foreach (var kvp in _valores)
            {
                clon._valores[kvp.Key] = kvp.Value;
            }
            return clon;
        }
    }
}
