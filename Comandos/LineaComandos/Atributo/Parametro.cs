using PER.Comandos.Tipos.Resultado;
using System.Reflection;

namespace PER.Comandos.LineaComandos.Atributo
{
    public class Parametro : IEquatable<Parametro>
    {
        public string Nombre { get; set; }
        public string Valor { get; set; }

        public Parametro()
        {
            Nombre = string.Empty;
            Valor = string.Empty;
        }

        public static object New<T>(IEnumerable<Parametro> parametros) where T : IParametro, new()
        {
            Type t = typeof(T);
            PropertyInfo[] propiedades = t.GetProperties();

            if (propiedades.Length == 0)
                throw new InvalidOperationException($"Tipo de dato {t.Name} no posee propiedades");

            if (parametros.Count() == 0)
                throw new InvalidOperationException($"No se pasaron parámetros");

            object? instancia = Activator.CreateInstance(t);
            if (instancia == null)
                throw new NullReferenceException($"No se pudo crear instancia de tipo de dato {t.Name}");

            foreach (PropertyInfo p in propiedades)
            {
                NombreAttribute? nombre = p.GetCustomAttribute<NombreAttribute>();
                if (nombre == null)
                    continue;

                nombre.Map(instancia, p, parametros);
            }

            IParametro ipar = (IParametro)instancia;
            Resultado res = ipar.ComprobarCombinacionParametros();
            if (!res.Ok)
                if (res.Error != null)
                    throw new Excepcion.ErrorDeSintaxisExcepcion(res.Error.Mensaje);
                else
                    throw new Excepcion.ErrorDeSintaxisExcepcion("Error en comprobación de parámetros");

            return instancia;
        }

        public bool Equals(Parametro? other)
        {
            if (other == null) return false;
            if (other == this) return true;

            return other.Nombre == Nombre && other.Valor == Valor;
        }
    }
}
