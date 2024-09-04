using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.LineaComandos.Excepcion;

namespace PER.Comandos.LineaComandos.FactoriaComandos
{
    public class FactoriaComandos<TRead, TWrite> : IFactoriaComandos<TRead, TWrite>
    {
        private IDictionary<string, Nodo<TRead, TWrite>> _nodos;

        public FactoriaComandos()
        {
            _nodos = new Dictionary<string, Nodo<TRead, TWrite>>();
        }

        public Nodo<TRead, TWrite> Add(string nombre)
        {
            var nodo = new Nodo<TRead, TWrite>();
            _nodos.Add(nombre, nodo);

            return nodo;
        }

        public Nodo<TRead, TWrite> Add(string nombre, Nodo<TRead, TWrite> nodo)
        {
            _nodos.Add(nombre, nodo);
            return nodo;
        }

        public IComando<TRead, TWrite> Crear(LineaComando lineaComando, IConfiguracion configuracion, ILogger logger)
        {
            var pila = new Stack<string>(lineaComando.Ruta.Reverse());
            return Crear(pila, lineaComando.Parametros, configuracion, logger);
        }

        private IComando<TRead, TWrite> Crear(Stack<string> lineaComando, ICollection<Parametro> parametros, IConfiguracion configuracion, ILogger logger)
        {
            if (lineaComando.Count == 0)
                throw new ColeccionVaciaExcepcion("línea de comandos no definido");

            string nombre = lineaComando.Pop();
            Nodo<TRead, TWrite> nodo;
            try
            {
                nodo = _nodos[nombre];
            }
            catch (KeyNotFoundException ex)
            {
                throw new NoEncontradoExcepcion($"nodo {nombre} no fue encontrado", ex);
            }
            catch (Exception)
            {
                throw;
            }

            return nodo.Crear(lineaComando, parametros, configuracion, logger);
        }
    }
}
