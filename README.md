# PER.Comandos

Librería para crear rápidamente un árbol de comandos y mapear los parámetros pasados por línea de comandos a estructuras de clase.

## Estructura de comandos

La estructura esperada por el objeto `PER.Comandos.LineaComandos.LineaComandos` es la siguiente (sin los corchetes):

1. [modulo1] [modulo2] [modulo3] [comando] [--parametro1=valor1] [--parametro2]
2. [modulo1] [modulo2] [modulo3] [comando] 
3. [comando] [--parametro1=valor1] [--parametro2]
4. [comando] 

Al inicializar el objeto, la respectiva ruta y parámetros quedan separados en los parámetros `Ruta` y `Parametros`.

## Configuracion

La clase abstracta `AConfiguracion` hereda la interfas `IConfiguracion` la cual está pensada para un objeto que sea capaz de acceder a la configuración del sistema. Debe ser heredada y creada por el programador.

## Nodo

La clase nodo `PER.Comandos.LineaComandos.FactoriaComandos.Nodo` es el que permite crear una estructura de árbol y cumple dos funciones:

1. Representa un nodo en el arbol.
2. Creador de un comando asociado a un nodo superior.

La manera de crear una ruta en el arbol y asociar dos comando al `nodo3` es la siguiente:

```c#
var nodo = new Nodo<string, string>();
nodo.Add("nodo1").Add("nodo2").Add("nodo3")
                .Add("comando1", new Nodo<string, string>(new ComandoPrueba1()))
                .Padre?.Add("comando2", new Nodo<string, string>(new ComandoPrueba2()));
```

Los nodos `comando1` y `comando2` cumplen la función de creadores de comandos, pero, si asocia nodos debajo de estos, también cumplirán la función de nodo en el arbol.

## Factoria de comandos

La clase `PER.Comandos.LineaComandos.FactoriaComandos.FactoriaComandos` utiliza la clase `Nodo` y este tiene la capacidad de mantener uno o más nodos raiz, pudiendo tener varias estructuras de arboles:

```c#
var factoria = new FactoriaComandos<string, string>();
factoria.Add("nodo1").Add("nodo2").Add("nodo3")
    .Add("comando1", new Nodo<string, string>(new ComandoPrueba1()))
    .Padre?.Add("comando2", new Nodo<string, string>(new ComandoPrueba2()));

factoria.Add("comando3", new Nodo<string, string>(new ComandoPrueba2()));

//lineaComando ya tuvo que ser inicializado con la ruta a buscar
IComando<string, string> comando = factoria.Crear(lineaComando, configuracion, logger);
```

## Preparación de comandos

La creación de nuevos comando se consigue heredando la clase abstracta `PER.Comandos.LineaComandos.Comando.ComandoBase` e implementando las funciones abstractas.

### Parámetros

Dentro del comando creado se espera parámetros pasados por el usuario los cuales ya fueron procesados en una colección por `LineaComandos`. Se puede tener una clase la cual tenga todos los parámetros esperados. Esta clase debe heredar a la interfaz `PER.Comandos.LineaComandos.Atributo.IParametro` y sus propiedades utilizar el atributo `NombreAttribute`:

```c#
public class ParametrosComando : IParametro 
{
    [Nombre("parametro1")]
    public string Parametro1 { get; set; }

    [Nombre("parametro2")]
    public bool Parametro2 { get; set; }
}

//Comando
private ParametrosComando _parametros;

public override void Preparar(ICollection<Parametro> parametros, IConfiguracion configuracion, ILogger logger) 
{
    _parametros = Parametro.New<ParametrosComando>(parametros);
    //Comprobacion de configuración si hace falta
    //asignación de logger si hace falta
}

```
