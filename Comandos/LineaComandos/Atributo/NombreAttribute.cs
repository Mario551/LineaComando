using PER.Comandos.LineaComandos.Excepcion;
using System.Globalization;
using System.Reflection;

namespace PER.Comandos.LineaComandos.Atributo
{
    public class NombreAttribute : Attribute
    {
        private string _nombre;

        public NombreAttribute(string nombre)
        {
            _nombre = nombre;
        }

        public void Map(object instancia, PropertyInfo propiedad, IEnumerable<Parametro> parametros)
        {
            Parametro? par = parametros.Where(p => p.Nombre == $"--{_nombre}").FirstOrDefault();
            if (par == null)
            {
                if (CompararTipos<bool, bool?>(propiedad.PropertyType))
                {
                    propiedad.SetValue(instancia, false);
                    return;
                }

                propiedad.SetValue(instancia, default);
                return;
            }

            if (CompararTipos<bool, bool?>(propiedad.PropertyType))
            {
                propiedad.SetValue(instancia, true);
                return;
            }

            if (string.IsNullOrEmpty(par.Valor))
                throw new ErrorDeSintaxisExcepcion($"El parámetro '{_nombre}' debe tener asignado un valor");

            try
            {
                if (CompararTipos<string, string?>(propiedad.PropertyType))
                {
                    propiedad.SetValue(instancia, par.Valor);
                    return;
                }

                if (CompararTipos<char, char?>(propiedad.PropertyType))
                {
                    propiedad.SetValue(instancia, par.Valor);
                    return;
                }

                if (CompararTipos<DateTime, DateTime?>(propiedad.PropertyType))
                {
                    DateTime fecha = StringToFecha(par.Valor, _nombre);
                    propiedad.SetValue(instancia, fecha);
                    return;
                }

                if (CompararTipos<byte, byte?>(propiedad.PropertyType))
                {
                    byte valor = byte.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }

                if (CompararTipos<short, short?>(propiedad.PropertyType))
                {
                    short valor = short.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }

                if (CompararTipos<int, int?>(propiedad.PropertyType))
                {
                    int valor = int.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }

                if (CompararTipos<long, long?>(propiedad.PropertyType))
                {
                    long valor = long.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }

                if (CompararTipos<decimal, decimal?>(propiedad.PropertyType))
                {
                    decimal valor = decimal.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }

                if (CompararTipos<double, double?>(propiedad.PropertyType))
                {
                    double valor = double.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }

                if (CompararTipos<float, float?>(propiedad.PropertyType))
                {
                    float valor = float.Parse(par.Valor);
                    propiedad.SetValue(instancia, valor);
                    return;
                }
            }
            catch (Exception ex)
            {
                throw new ErrorDeSintaxisExcepcion($"Error al procesar el parámetro {_nombre}", ex);
            }

        }

        private bool CompararTipos<T, TNULL>(Type cmp)
        {
            return cmp == typeof(T) || cmp == typeof(TNULL);
        }

        private DateTime StringToFecha(string strFecha, string nombreParametro)
        {
            DateTime fecha;
            try
            {
                if (strFecha.Length > 10)
                    fecha = DateTime.ParseExact(strFecha, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.CurrentCulture);
                else
                    fecha = DateTime.ParseExact(strFecha, "yyyy-MM-dd", CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                throw new ErrorDeSintaxisExcepcion($"el parámetro --{nombreParametro} debe poseer el formato: yyyy-MM-dd", ex);
            }

            return fecha;
        }
    }
}
