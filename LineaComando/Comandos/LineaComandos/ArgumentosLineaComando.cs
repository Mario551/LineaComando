using System.Text;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Excepcion;

namespace PER.Comandos.LineaComandos
{
    public static class ArgumentosLineaComando
    {
        public const string NombreParametroData = "--data";

        public static ResultadoArgumentosLineaComando Parsear(string argumentos)
        {
            ArgumentNullException.ThrowIfNull(argumentos);

            List<Parametro> parametros = new List<Parametro>();
            string? data = null;
            bool dataEncontrada = false;
            ReadOnlyMemory<char> continuacion = argumentos.AsMemory();

            while (true)
            {
                continuacion = OmitirEspaciosIniciales(continuacion);
                if (continuacion.IsEmpty)
                    break;

                (Parametro parametro, continuacion) = SepararParametro(continuacion);
                AgregarParametro(parametro, parametros, ref data, ref dataEncontrada);
            }

            return new ResultadoArgumentosLineaComando
            {
                Parametros = parametros,
                Data = data
            };
        }

        public static ResultadoArgumentosLineaComando Parsear(
            ICollection<string> argumentosTokenizados)
        {
            ArgumentNullException.ThrowIfNull(argumentosTokenizados);

            List<Parametro> parametros = new List<Parametro>();
            string? data = null;
            bool dataEncontrada = false;

            foreach (string argumento in argumentosTokenizados)
            {
                if (string.IsNullOrEmpty(argumento))
                    throw new ErrorDeSintaxisExcepcion("Se recibió un argumento vacío.");

                int indiceIgual = argumento.IndexOf('=');
                string nombre = indiceIgual < 0
                    ? argumento
                    : argumento[..indiceIgual];
                ValidarNombre(nombre);

                string? valor = null;
                if (indiceIgual >= 0)
                    valor = argumento[(indiceIgual + 1)..];

                Parametro parametro = new Parametro
                {
                    Nombre = nombre,
                    Valor = valor
                };
                AgregarParametro(parametro, parametros, ref data, ref dataEncontrada);
            }

            return new ResultadoArgumentosLineaComando
            {
                Parametros = parametros,
                Data = data
            };
        }

        public static string Serializar(IEnumerable<Parametro> parametros)
        {
            ArgumentNullException.ThrowIfNull(parametros);

            StringBuilder resultado = new StringBuilder();
            bool dataEncontrada = false;

            foreach (Parametro parametro in parametros)
            {
                if (parametro is null)
                    throw new ArgumentException("La colección contiene un parámetro nulo.", nameof(parametros));

                ValidarNombre(parametro.Nombre);

                bool esData = EsData(parametro.Nombre);
                if (esData)
                {
                    if (dataEncontrada)
                        throw new ErrorDeSintaxisExcepcion("Solo se permite un parámetro --data.");

                    if (parametro.Valor is null)
                        throw new ErrorDeSintaxisExcepcion("El parámetro '--data' debe tener un valor asignado.");

                    dataEncontrada = true;
                }

                if (resultado.Length > 0)
                    resultado.Append(' ');

                resultado.Append(parametro.Nombre);
                if (parametro.Valor is null)
                    continue;

                resultado.Append('=');
                if (esData || RequiereAgrupacion(parametro.Valor))
                {
                    resultado.Append('\'');
                    AgregarValorEscapado(resultado, parametro.Valor, '\'');
                    resultado.Append('\'');
                }
                else
                {
                    resultado.Append(parametro.Valor);
                }
            }

            return resultado.ToString();
        }

        private static (Parametro Parametro, ReadOnlyMemory<char> Continuacion) SepararParametro(ReadOnlyMemory<char> argumentos)
        {
            ReadOnlySpan<char> span = argumentos.Span;
            int indiceFinNombre = 0;

            while (indiceFinNombre < span.Length
                && span[indiceFinNombre] != '='
                && span[indiceFinNombre] != ' ')
            {
                indiceFinNombre++;
            }

            string nombre = span[..indiceFinNombre].ToString();
            ValidarNombre(nombre);

            if (indiceFinNombre == span.Length || span[indiceFinNombre] == ' ')
            {
                Parametro flag = new Parametro { Nombre = nombre };
                return (flag, argumentos[indiceFinNombre..]);
            }

            int indiceInicioValor = indiceFinNombre + 1;
            if (indiceInicioValor == span.Length)
            {
                throw new ErrorDeSintaxisExcepcion(
                    $"El parámetro '{nombre}' no tiene un valor asignado.");
            }

            char primerCaracterValor = span[indiceInicioValor];
            bool tieneDelimitador = primerCaracterValor is '\'' or '"';

            if (!tieneDelimitador)
            {
                if (EsData(nombre))
                    throw new ErrorDeSintaxisExcepcion("El parámetro '--data' debe estar delimitado por comillas simples o dobles.");

                int longitudValor = 0;
                while (indiceInicioValor + longitudValor < span.Length
                    && span[indiceInicioValor + longitudValor] != ' ')
                {
                    longitudValor++;
                }

                string valor = span.Slice(indiceInicioValor, longitudValor).ToString();
                Parametro parametro = new Parametro
                {
                    Nombre = nombre,
                    Valor = valor
                };
                return (parametro, argumentos[(indiceInicioValor + longitudValor)..]);
            }

            char delimitador = primerCaracterValor;
            bool escapado = false;
            int indiceCierre = -1;

            for (int i = indiceInicioValor + 1; i < span.Length; i++)
            {
                char caracter = span[i];
                if (caracter == '\\')
                {
                    escapado = !escapado;
                    continue;
                }

                if (caracter == delimitador && !escapado)
                {
                    indiceCierre = i;
                    break;
                }

                if (escapado)
                    escapado = false;
            }

            if (indiceCierre < 0)
                throw new ErrorDeSintaxisExcepcion($"El parámetro '{nombre}' no tiene un delimitador de cierre `{delimitador}`.");

            int indiceContinuacion = indiceCierre + 1;
            if (indiceContinuacion < span.Length && span[indiceContinuacion] != ' ')
                throw new ErrorDeSintaxisExcepcion($"El parámetro '{nombre}' contiene texto después de su delimitador de cierre.");

            ReadOnlySpan<char> valorAgrupado = span.Slice(indiceInicioValor + 1, indiceCierre - indiceInicioValor - 1);

            Parametro parametroAgrupado = new Parametro
            {
                Nombre = nombre,
                Valor = DesescaparValor(valorAgrupado, delimitador)
            };

            return (parametroAgrupado, argumentos[indiceContinuacion..]);
        }

        private static ReadOnlyMemory<char> OmitirEspaciosIniciales(ReadOnlyMemory<char> argumentos)
        {
            int cantidad = 0;
            while (cantidad < argumentos.Length && argumentos.Span[cantidad] == ' ')
                cantidad++;

            return argumentos[cantidad..];
        }

        private static void AgregarParametro(
            Parametro parametro,
            ICollection<Parametro> parametros,
            ref string? data,
            ref bool dataEncontrada)
        {
            if (!EsData(parametro.Nombre))
            {
                parametros.Add(parametro);
                return;
            }

            if (dataEncontrada)
                throw new ErrorDeSintaxisExcepcion("Solo se permite un parámetro --data.");

            if (parametro.Valor is null)
                throw new ErrorDeSintaxisExcepcion("El parámetro '--data' debe tener un valor asignado.");

            data = parametro.Valor;
            dataEncontrada = true;
        }

        private static bool EsData(string nombre)
        {
            return string.Equals(nombre, NombreParametroData, StringComparison.Ordinal);
        }

        private static void ValidarNombre(string nombre)
        {
            if (nombre.Length <= 2
                || !nombre.StartsWith("--", StringComparison.Ordinal)
                || nombre[2] == '-'
                || nombre.Any(caracter => char.IsWhiteSpace(caracter)
                    || caracter is '=' or '\'' or '"'))
            {
                throw new ErrorDeSintaxisExcepcion(
                    $"El nombre de parámetro '{nombre}' no es válido.");
            }
        }

        private static bool RequiereAgrupacion(string valor)
        {
            return valor.Length == 0
                || valor[0] is '\'' or '"'
                || valor.Any(caracter => char.IsWhiteSpace(caracter)
                    || caracter is '\\' or '\'');
        }

        private static void AgregarValorEscapado(
            StringBuilder resultado,
            string valor,
            char delimitador)
        {
            foreach (char caracter in valor)
            {
                if (caracter == '\\' || caracter == delimitador)
                    resultado.Append('\\');

                resultado.Append(caracter);
            }
        }

        private static string DesescaparValor(ReadOnlySpan<char> valor, char delimitador)
        {
            int indiceEscape = valor.IndexOf('\\');
            if (indiceEscape < 0)
                return valor.ToString();

            StringBuilder resultado = new StringBuilder(valor.Length);
            for (int i = 0; i < valor.Length; i++)
            {
                char caracter = valor[i];
                if (caracter == '\\'
                    && i + 1 < valor.Length
                    && (valor[i + 1] == '\\' || valor[i + 1] == delimitador))
                {
                    resultado.Append(valor[++i]);
                    continue;
                }

                resultado.Append(caracter);
            }

            return resultado.ToString();
        }
    }
}
