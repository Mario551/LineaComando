using PER.Comandos.Tipos.Resultado;
using System.Reflection;

namespace PER.Comandos.LineaComandos.Configuracion
{
    public abstract class AConfiguracion : IConfiguracion
    {
        public event EventHandler<ArgsEventoConfiguracionCargada>? EventoConfiguracionCargada;

        protected void __OnEventoConfiguracionCargada(IConfiguracion configuracion)
            => EventoConfiguracionCargada?.Invoke(this, new ArgsEventoConfiguracionCargada(configuracion));

        public virtual Resultado<T> Get<T>(string nombre)
        {
            Type type = GetType();
            PropertyInfo? propertyInfo = type.GetProperty(nombre);
            if (propertyInfo == null)
                return new Resultado<T>(false)
                {
                    Error = new Error($"Propiedad `{nombre}` no encontrada")
                };

            object? valor = propertyInfo.GetValue(this);
            if (valor == null)
                return new Resultado<T>(false)
                {
                    Error = new Error($"Propiedad `{nombre}` tiene un valor null")
                };

            if (!(valor is T))
            {
                Type typeT = typeof(T);
                return new Resultado<T>(false)
                {
                    Error = new Error($"Propiedad `{nombre}` no tiene un valor de tipo `{typeT.Name}`")
                };
            }

            T res = (T)valor;
            return new Resultado<T>(true, res);
        }

        public abstract object Clone();
    }
}
