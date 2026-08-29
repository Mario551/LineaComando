using PER.Comandos.LineaComandos.Atributo;

namespace PER.Comandos.LineaComandos
{
    /// <summary>
    /// Obtiene la ruta de ejecución del comando y sus parámetros a partir de una colección de texto con el siguiente formato: [modulo1] [modulo2] [comando] [--parametro1=valor1] [--parametro2]
    /// </summary>
    public class LineaComando
    {
        public ICollection<string> Ruta { get; set; }
        public ICollection<Parametro> Parametros { get; set; }
        public string? Data { get; set; }

        public LineaComando(ICollection<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (!args.Any())
                throw new ArgumentException("Colección de argumentos vacía");

            int pos = args
                .Select((argumento, indice) => new { argumento, indice })
                .Where(elemento => elemento.argumento.StartsWith("--", StringComparison.Ordinal))
                .Select(elemento => elemento.indice)
                .DefaultIfEmpty(-1)
                .First();

            //Comando sin parámetros
            if (pos == -1)
            {
                Ruta = args.ToList();
                Parametros = new List<Parametro>();
                return;
            }

            ResultadoArgumentosLineaComando argumentos = ArgumentosLineaComando.Parsear(
                args.Skip(pos).ToList());

            Ruta = args.Take(pos).ToList();
            Parametros = argumentos.Parametros;
            Data = argumentos.Data;
        }

        public LineaComando(
            ICollection<string> ruta,
            ICollection<Parametro> parametros,
            string? data = null)
        {
            ArgumentNullException.ThrowIfNull(ruta);
            ArgumentNullException.ThrowIfNull(parametros);

            if (!ruta.Any())
                throw new ArgumentException("Colección de ruta vacía", nameof(ruta));

            Ruta = ruta.ToList();
            Parametros = parametros.ToList();
            Data = data;
        }
    }
}
