using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Configuracion;
using PER.Comandos.LineaComandos.Excepcion;

namespace PER.Comandos.LineaComandos.FactoriaComandos
{
    public class Nodo<TRead, TWrite> : IComandoCreador<TRead, TWrite>
    {
        public Nodo<TRead, TWrite>? Padre { get; private set; }

        private IDictionary<string, Nodo<TRead, TWrite>> _nodos;
        private Func<ICollection<Parametro>, IConfiguracion, ILogger, IComando<TRead, TWrite>>? _creador;
        private ComandoBase<TRead, TWrite>? _comando = null;
        private Func<CancellationToken, Task>? _comenzar;
        private Func<CancellationToken, Task>? _finalizar;

        public Nodo()
        {
            _nodos = new Dictionary<string, Nodo<TRead, TWrite>>();
        }

        public Nodo(Func<ICollection<Parametro>, IConfiguracion, ILogger, IComando<TRead, TWrite>> creador) : this()
        {
            _creador = creador;
        }

        public Nodo(ComandoBase<TRead, TWrite> comando) : this()
        {
            _comando = comando;
        }

        /// <summary>
        /// Agrega un nodo hijo al padre y lo asocia a un nombre
        /// </summary>
        /// <param name="nombre">Nombre del nodo</param>
        /// <param name="nodo">Nodo a agregar al padre</param>
        /// <returns>Retorna el nodo creado</returns>
        public Nodo<TRead, TWrite> Add(string nombre, Nodo<TRead, TWrite> nodo)
        {
            nodo.Padre = this;
            _nodos.Add(nombre, nodo);
            return nodo;
        }

        /// <summary>
        /// Agrega un nodo hijo al padre y lo asocia a un nombre
        /// </summary>
        /// <param name="nombre">Nombre del nodo</param>
        /// <param name="nodo">Nodo a agregar al padre</param>
        /// <returns>Retorna el nodo creado</returns>
        public Nodo<TRead, TWrite> Add(string nombre)
        {
            var nodo = new Nodo<TRead, TWrite>();
            nodo.Padre = this;
            _nodos.Add(nombre, nodo);
            return nodo;
        }

        /// <summary>
        /// Retorna un nodo hijo que concuerde con el nombre
        /// </summary>
        /// <param name="nombre"></param>
        /// <returns>Nodo asociado al nombre</returns>
        public Nodo<TRead, TWrite> Get(string nombre)
            => _nodos[nombre];

        public IComando<TRead, TWrite> Crear(Stack<string> lineaComando, ICollection<Parametro> parametros, IConfiguracion configuracion, ILogger logger)
        {
            IComando<TRead, TWrite>? comando = null;

            if (lineaComando.Count > 0)
            {
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

                comando = nodo.Crear(lineaComando, parametros, configuracion, logger);
            }
            else if (_creador != null)
            {
                comando = _creador(parametros, configuracion, logger);
                if (_comenzar != null)
                    comando.EmpezarCon(_comenzar);

                if (_finalizar != null)
                    comando.FinalizarCon(_finalizar);
            }
            else if (_comando != null)
            {
                _comando.Preparar(parametros, configuracion, logger);
                comando = _comando;

                if (_comenzar != null)
                    comando.EmpezarCon(_comenzar);

                if (_finalizar != null)
                    comando.FinalizarCon(_finalizar);
            }
            else
                throw new NullReferenceException("Creador no definido");

            return comando;
        }

        public IComandoCreador<TRead, TWrite> EmpezarCon(Func<CancellationToken, Task> comenzar)
        {
            _comenzar = comenzar;
            return this;
        }

        public IComandoCreador<TRead, TWrite> FinalizarCon(Func<CancellationToken, Task> finalizar)
        {
            _finalizar = finalizar;
            return this;
        }
    }
}
