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

        public LineaComando(ICollection<string> args) 
        {
            if (!args.Any())
                throw new ArgumentException("Colección de argumentos vacía");

            int pos = -1;
            for (int i = 0; i < args.Count; i++)
            {
                if (args.ElementAt(i).Substring(0, 2) == "--")
                {
                    pos = i;
                    break;
                }
            }

            this.Parametros = new List<Parametro>();

            //Comando sin parámetros
            if (pos == -1)
            {
                this.Ruta = args;
                return;
            }

            for (int i = pos; i < args.Count; i++)
            {
                string arg = args.ElementAt(i);
                string[] keyValor = arg.Split('=');
                if (keyValor.Length == 2)
                    Parametros.Add(new Parametro
                    {
                        Nombre = keyValor[0],
                        Valor = keyValor[1]
                    });
                else
                    Parametros.Add(new Parametro
                    {
                        Nombre = keyValor[0]
                    });
            }

            this.Ruta = args.Take(pos).ToList();
        }
    }
}
