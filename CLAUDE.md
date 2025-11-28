# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Descripción del Proyecto

PER.Comandos es una librería C# .NET 6.0 para crear aplicaciones de línea de comandos con árboles de comandos jerárquicos y mapeo automático de parámetros. La librería sigue un patrón factory para construir jerarquías de comandos estructuradas en árbol.

**Versión actual:** 1.0.1
**Target Framework:** .NET 6.0
**Repositorio:** https://github.com/Mario551/LineaComando

## Comandos de Compilación y Pruebas

```bash
# Compilar la librería principal
dotnet build Comandos/Comandos.csproj

# Compilar el proyecto de pruebas
dotnet build Test/ComandosTest/ComandosTest.csproj

# Compilar ambos proyectos
dotnet build Comandos/Comandos.sln

# Ejecutar todas las pruebas
dotnet test Test/ComandosTest/ComandosTest.csproj

# Ejecutar pruebas con salida detallada
dotnet test Test/ComandosTest/ComandosTest.csproj --verbosity normal

# Limpiar artefactos de compilación
dotnet clean Comandos/Comandos.sln

# Restaurar dependencias NuGet
dotnet restore Comandos/Comandos.sln

# Crear paquete NuGet (Release)
dotnet pack Comandos/Comandos.csproj --configuration Release
```

## Arquitectura Principal

### Estructura del Árbol de Comandos
La librería implementa un sistema de comandos jerárquico donde los comandos se organizan en una estructura de árbol:
- **Nodo<TRead, TWrite>**: Nodos del árbol que pueden contener nodos hijos o creadores de comandos
- **FactoriaComandos<TRead, TWrite>**: Factory que administra nodos raíz y crea comandos
- **LineaComando**: Analiza argumentos de línea de comandos en ruta y parámetros

### Componentes Clave

1. **Flujo de Creación de Comandos**:
   - `LineaComando` analiza argumentos en `Ruta` (ruta) y `Parametros` (parámetros)
   - `FactoriaComandos` usa la ruta para navegar la estructura del árbol
   - Las instancias de `Nodo` contienen nodos hijos o creadores de comandos
   - Los comandos se crean en nodos hoja e se inicializan con parámetros

2. **Implementación de Comandos**:
   - Todos los comandos heredan de `ComandoBase<TRead, TWrite>`
   - Deben implementar `Preparar()` para procesamiento de parámetros y `EjecutarAsync()` para ejecución
   - Soportan middleware con hooks `EmpezarCon()` y `FinalizarCon()`

3. **Mapeo de Parámetros**:
   - Las clases de parámetros implementan la interfaz `IParametro`
   - Las propiedades usan el atributo `[Nombre("param-name")]` para mapeo CLI
   - Usar `Parametro.New<T>(parametros)` para mapear parámetros CLI a objetos fuertemente tipados

### Formato de Línea de Comandos
Formato esperado: `[modulo1] [modulo2] [comando] [--param1=valor1] [--param2]`

## Estructura del Proyecto

- **Comandos/**: Código fuente de la librería principal
  - **LineaComandos/**: Lógica principal de procesamiento de comandos
    - **Atributo/**: Atributos e interfaces de mapeo de parámetros
    - **Comando/**: Clases base e interfaces de comandos
    - **FactoriaComandos/**: Implementación de factory de comandos y nodos del árbol
    - **Configuracion/**: Administración de configuración
    - **Stream/**: Abstracciones de flujos de E/S
    - **Excepcion/**: Tipos de excepciones personalizadas
    - **EventSourcing/**: Sistema de event sourcing (en desarrollo)
      - **Bus/**: Bus de eventos en memoria
      - **Cola/**: Cola de comandos asíncronos
      - **Evento/**: Definiciones de eventos del sistema
      - **Handler/**: Interfaces para manejadores de eventos
      - **Outbox/**: Patrón Outbox para persistencia de eventos
      - **Procesadores/**: Procesadores de eventos y tareas programadas
      - **Publicador/**: Publicadores de eventos
      - **Registro/**: Registro de comandos y manejadores
  - **Datos/**: Capa de persistencia
    - **Entidades/**: Entidades para mapeo con base de datos (Entity Framework)
- **Test/ComandosTest/**: Proyecto de pruebas XUnit con Moq para mocking

## Archivos Clave

- `LineaComando.cs:13`: Lógica principal de análisis de argumentos
- `FactoriaComandos.cs:34`: Punto de entrada para creación de comandos
- `Nodo.cs:89`: Navegación del árbol e instanciación de comandos
- `ComandoBase.cs:35-36`: Métodos abstractos que todos los comandos deben implementar (`Preparar()` y `EjecutarAsync()`)

## Flujo de Ejecución Completo

```
Usuario ejecuta comando
    ↓
LineaComando.Parse(args)
    → Ruta: ["modulo1", "modulo2", "comando"]
    → Parametros: [--param1=valor1, --param2]
    ↓
FactoriaComandos.Crear(lineaComando, config, logger)
    → Navega árbol usando Ruta
    → Encuentra nodo "comando"
    → Crea instancia del comando
    ↓
Comando.Preparar(parametros, config, logger)
    → Mapea parámetros usando Parametro.New<T>()
    → Valida configuración
    → Inicializa logger
    ↓
[Opcional] Comando._comenzar() (middleware EmpezarCon)
    ↓
Comando.EjecutarAsync(stream, token)
    → Lógica principal del comando
    → Usa IStream<TRead, TWrite> para E/S
    ↓
[Opcional] Comando._finalizar() (middleware FinalizarCon)
```

## Patrones de Event Sourcing (En Desarrollo)

El proyecto está integrando un sistema completo de event sourcing:

### Componentes Principales

1. **Bus de Eventos (`IBusEventos`, `BusEventosEnMemoria`)**:
   - Sistema pub/sub para eventos en memoria
   - Permite suscripción y publicación de eventos

2. **Cola de Comandos (`IAlmacenColaComandos`, `ComandoEnCola`)**:
   - Ejecución asíncrona de comandos
   - Almacenamiento del resultado de comandos (`ResultadoComando`)

3. **Patrón Outbox (`IAlmacenOutbox`, `EventoOutbox`)**:
   - Persistencia confiable de eventos
   - Garantiza entrega eventual de eventos

4. **Procesadores**:
   - `ProcesadorEventos`: Procesa eventos del bus
   - `ProcesadorColaComandos`: Procesa comandos en cola
   - `CoordinadorTareasProgramadas`: Coordina tareas programadas

5. **Handlers (`IManejadorEvento`, `IManejadorProgramado`)**:
   - Interfaces para manejar eventos específicos
   - Soporte para tareas programadas

6. **Registro (`IRegistroComandos`, `IRegistroManejadores`)**:
   - Registro de comandos disponibles (`RegistroComandosJson`)
   - Registro de manejadores y sus configuraciones
   - Metadatos de comandos (`MetadatosComando`)

### Entidades de Persistencia

La capa `Comandos/Datos/Entidades/` incluye:
- `EventoOutboxEntidad`: Eventos pendientes de publicar
- `ManejadorEvento`: Configuración de manejadores
- `DisparadorManejador`: Disparadores para ejecutar manejadores
- `ComandoRegistrado`: Registro de comandos en el sistema
- `TipoEvento`: Tipos de eventos del sistema
- `EjecucionManejador`: Historial de ejecuciones

## Consideraciones de Desarrollo

### Middleware y Hooks
Los comandos soportan middleware mediante:
- `EmpezarCon(Func)`: Se ejecuta antes de `EjecutarAsync()`
- `FinalizarCon(Func)`: Se ejecuta después de `EjecutarAsync()`

### Sistema de Streams
Abstracción flexible para E/S:
- `IStream<TRead, TWrite>`: Base genérica
- `IStreamReader<TRead>`: Solo lectura
- `IStreamWriter<TWrite>`: Solo escritura
- `IStreamDoble<TRead, TWrite>`: Bidireccional
- `StreamChannel<TRead, TWrite>`: Implementación con canales
- `StreamDoble<TRead, TWrite>`: Implementación doble stream

Permite diferentes tipos de E/S (consola, red, archivo) sin cambiar el código del comando.

### Configuración
- `IConfiguracion`: Interfaz para acceso a configuración del sistema
- `AConfiguracion`: Clase base abstracta con evento `ConfiguracionCargada`
- Debe ser implementada por el usuario de la librería