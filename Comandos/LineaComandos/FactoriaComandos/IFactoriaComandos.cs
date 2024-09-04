using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Configuracion;

namespace PER.Comandos.LineaComandos.FactoriaComandos
{
    public interface IFactoriaComandos<TRead, TWrite>
    {
        /// <summary>
        /// Agrega un arbol de comandos a la factoria
        /// </summary>
        /// <param name="nombre">Nombre del primer nodo del arbol de comandos</param>
        /// <returns>Retorna el nodo creado</returns>
        Nodo<TRead, TWrite> Add(string nombre);

        /// <summary>
        /// Agrega un arbol de comandos a la factoria
        /// </summary>
        /// <param name="nombre">Nombre del primer nodo del arbol de comandos</param>
        /// <param name="nodo">Prime nodo del arbol de comandos</param>
        /// <returns>Retorna el nodo que fue pasado por parámetros</returns>
        Nodo<TRead, TWrite> Add(string nombre, Nodo<TRead, TWrite> nodo);

        /// <summary>
        /// Recibe una ruta de búsqueda en el arbol de comandos para la creación del comando. Ejemplo:
        ///     [módulo].[submodulo].[submodulo].[nombrecomando] --parametro1 --parametro2=valor
        /// </summary>
        /// <param name="lineaComando">Ruta de búsqueda en el arbol. Cada sección es un item en la colección</param>
        /// <param name="parametros">Parámetros de ejecución del comando</param>
        /// <param name="configuracion">Configuración que se desea pasar al comando</param>
        /// <param name="logger">Log del sistema</param>
        /// <returns></returns>
        IComando<TRead, TWrite> Crear(LineaComando lineaComando, IConfiguracion configuracion, ILogger logger);
    }
}
