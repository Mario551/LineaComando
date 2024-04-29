using PER.Comandos.LineaComandos.Atributo;

namespace PER.Comandos.LineaComandos
{
    public class Linea
    {
        public ICollection<string> Comando { get; set; }
        public ICollection<Parametro> Parametros { get; set; }
    }
}
