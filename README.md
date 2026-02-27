# PER.Comandos.LineaComandos

Sistema de procesamiento de comandos y eventos para aplicaciones .NET. Incluye cola de comandos asincronos y arquitectura event-driven con outbox pattern.

## Dependencias

- .NET 8.0+
- PostgreSQL (Npgsql), SQL Server (Microsoft.Data.SqlClient) o SQLite
- Dapper
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection


## Instalacion

Agregar referencias a los proyectos necesarios:

```xml
<ItemGroup>
    <ProjectReference Include="..\LineaComando.Builder\LineaComando.Builder.csproj" />
</ItemGroup>
```

## Configuracion con el Builder

### ASP.NET Core

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLineaComando(async (sp, builderInicializador, token) =>
    {
        // IBuilderComando, IBuilderManejador, etc. se obtienen de ServiceProvider
        // Usa builderInicializador.NewBuilderComando() para crear nuevos builders
        // Usa builderInicializador.NewBuilderTipoEvento() para crear builders de tipos de evento

        IBuilderComando builderComando = builderInicializador.NewBuilderComando();

        IBuilderManejador builderManejador = await builderComando
            .Argumentos("orden pagar", "Procesa el pago de una orden")
            .Accion(new PagarOrdenComando())
            .RegistrarAsync();

        IBuilderTipoEvento builderTipoEvento = builderInicializador.NewBuilderTipoEvento();
        ITipoEvento tipoEvento = await builderTipoEvento
            .Argumentos("ORDEN_CREADA", "Orden Creada", "Se emite cuando se crea una nueva orden")
            .RegistrarAsync();

        IBuilderDisparador builderDisparador = await builderManejador
            .Argumentos("NOTIFICAR_ORDEN_CREADA", "Notificar orden creada", string.Empty, "Envia notificacion al crear una orden")
            .RegistrarAsync();

        await builderDisparador
            .Argumentos("DISPARADOR_ORDEN_CREADA", 1, tipoEvento)
            .RegistrarAsync();
    })
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .SetTiempoRefrescoColaComandos(TimeSpan.FromSeconds(1))
    .SetMaxParalelismoCola(4)
    .Build();

var app = builder.Build();

await app.InicializarLineaComandoAsync();

app.Run();
```

### Aplicacion de Consola con Host

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services
            .AddLineaComando(async (sp, builderInicializador, token) =>
            {
                // Configura comandos, manejadores, eventos usando los builders
                IBuilderComando builderComando = builderInicializador.NewBuilderComando();

                await builderComando
                    .Argumentos("orden pagar", "Procesa el pago de una orden")
                    .Accion(new PagarOrdenComando())
                    .RegistrarAsync();
            })
            .UsePostgresql(context.Configuration.GetConnectionString("Default")!)
            .SetTiempoRefrescoColaComandos(TimeSpan.FromSeconds(1))
            .SetMaxParalelismoCola(4)
            .Build();
    })
    .Build();

await host.InicializarLineaComandoAsync();
await host.RunAsync();
```

### Seleccion de Base de Datos

El builder permite elegir entre PostgreSQL, SQL Server o SQLite mediante los metodos:

- `.UsePostgresql(connectionString)` - Para PostgreSQL
- `.UseSqlServer(connectionString)` - Para SQL Server
- `.UseSqlite(connectionString)` - Para SQLite

La implementacion concreta de las interfaces se registra automaticamente en el contenedor de dependencias segun la base de datos seleccionada.

### Configuracion de Parametros

| Metodo | Descripcion | Valor por Defecto |
|--------|-------------|-------------------|
| `SetTiempoRefrescoColaComandos(TimeSpan)` | Intervalo de escaneo de la cola de comandos | 1 segundo |
| `SetTiempoRefrescoColaEventos(TimeSpan)` | Intervalo de escaneo de eventos pendientes | 1 segundo |
| `SetTiempoRefrescoColaTareas(TimeSpan)` | Intervalo de verificacion de tareas programadas | 1 segundo |
| `SetMaxParalelismoCola(int)` | Maximo de comandos ejecutandose en paralelo | 4 |

## Servicios en Segundo Plano

Al llamar `Build()`, se registran automaticamente los siguientes `BackgroundService`:

| Servicio | Descripcion |
|----------|-------------|
| `ServicioColaComandos` | Escanea la tabla `per_cola_comandos`, obtiene comandos pendientes y los ejecuta en paralelo |
| `ServicioProcesadorEventos` | Escanea la tabla `per_eventos_outbox`, lee eventos pendientes y encola comandos asociados a sus manejadores |
| `ServicioTareasProgramadas` | Verifica manejadores con expresiones cron y encola comandos cuando corresponde |

---

## Cola de Comandos

### Crear un Comando

Un comando hereda de `ComandoBase<TRead, TWrite>`:

```csharp
public class PagarOrdenComando : ComandoBase<string, ResultadoComando>
{
    private PagarOrdenParametros _parametros;

    public override void Preparar(
        ICollection<Parametro> parametros,
        IConfiguracion configuracion,
        ILogger logger)
    {
        _parametros = Parametro.New<PagarOrdenParametros>(parametros);
    }

    public override async Task EjecutarAsync(
        IStream<string, ResultadoComando> stream,
        CancellationToken token = default)
    {
        var datos = stream.ObtenerEntrada();

        // Logica del comando...

        stream.Escribir(ResultadoComando.Exito("Orden pagada"));
    }
}

public class PagarOrdenParametros : IParametro
{
    [Nombre("ordenId")]
    public long OrdenId { get; set; }

    [Nombre("monto")]
    public decimal Monto { get; set; }
}
```

### Registrar un Comando

```csharp
// IRegistroComandos se obtiene via ServiceProvider (inyeccion de dependencias)
IRegistroComandos<string, ResultadoComando> registroComandos = /* desde DI */;

await registroComandos.RegistrarComandoAsync(
    new MetadatosComando
    {
        RutaComando = "orden pagar",
        Descripcion = "Procesa el pago de una orden"
    },
    new Nodo<string, ResultadoComando>(new PagarOrdenComando()),
    token);
```

### Encolar un Comando

Usa `IAlmacenColaComandos` para encolar comandos:

```csharp
public class MiServicio
{
    private readonly IAlmacenColaComandos _almacenCola;

    public MiServicio(IAlmacenColaComandos almacenCola)
    {
        _almacenCola = almacenCola;
    }

    public async Task ProcesarPagoAsync(long ordenId, decimal monto)
    {
        var comando = new ComandoEnCola
        {
            RutaComando = "orden pagar",
            Argumentos = $"--ordenId={ordenId} --monto={monto}",
            DatosDeComando = JsonSerializer.Serialize(new { OrdenId = ordenId, Monto = monto })
        };

        await _almacenCola.EncolarAsync(comando);
    }
}
```

El `ServicioColaComandos` recogera automaticamente el comando y lo ejecutara.

---

## Event-Driven

### Flujo de Eventos

```
[Tu Codigo] -> IRegistroEventoBuilder.Argumentos(tipoEvento, datos, agregadoId).RegistrarEnColaAsync()
                                                              |
                                                              v
[ServicioProcesadorEventos] <- ObtenerEventosPendientesAsync() lee EventoOutbox
            |
            v
    Agrega parametros automaticos:
    --origen=evento
    --codigo={CodigoTipoEvento}
    --agregado-id={valor} (solo si AgregadoId tiene valor)
            |
            v
    ObtenerManejadoresParaEventoAsync() por CodigoTipoEvento
            |
            v
    Por cada manejador -> IAlmacenColaComandos.EncolarAsync(ComandoEnCola)
            |
            v
[ServicioColaComandos] ejecuta el comando con todos los argumentos
```

**Parametros Automaticos**: Cuando un comando se ejecuta a traves del sistema event-driven, se agregan automaticamente los siguientes parametros:

| Parametro | Descripcion | Ejemplo |
|-----------|-------------|---------|
| `--origen` | Indica el origen: `evento` o `disparador` | `--origen=evento` |
| `--codigo` | Codigo del tipo de evento o del disparador | `--codigo=ORDEN_CREADA` |
| `--agregado-id` | ID del agregado (solo para eventos con AgregadoId) | `--agregado-id=123` |

Estos parametros se antepone a los argumentos configurados en el manejador, permitiendo al comando conocer su contexto de ejecucion.

### Modelo de Datos Event-Driven

El sistema event-driven utiliza tres tablas relacionadas:

```
+------------------+       +-----------------------+       +----------------------------+
|per_tipos_evento  |       | per_manejadores_evento|       | per_disparadores_manejador |
+------------------+       +-----------------------+       +----------------------------+
| id (PK)          |       | id (PK)               |       | id (PK)                    |
| codigo           |       | codigo                |       | manejador_evento_id(FK)    |---> per_manejadores_evento
| nombre           |       | nombre                |       | tipo_evento_id (FK)        |---> per_tipos_evento
| descripcion      |       | descripcion           |       | modo_disparo               |
| activo           |       | ruta_comando          |       | expresion                  |
| creado_en        |       | argumentos_comando    |       | activo                     |
+------------------+       | activo                |       | prioridad                  |
                           | creado_en             |       | ultima_ejecucion           |
                           +-----------------------+       | creado_en                  |
                                                           +----------------------------+
```

**per_tipos_evento**: Catalogo de eventos que pueden ocurrir en tu sistema. Define QUE cosas pueden pasar (ej: "ORDEN_CREADA", "PAGO_RECIBIDO", "USUARIO_REGISTRADO").

**per_manejadores_evento**: Define QUE COMANDO ejecutar como reaccion. Cada manejador tiene una `ruta_comando` que apunta a un comando registrado en la cola.

**per_disparadores_manejador**: Es el PUENTE que conecta todo. Define CUANDO se dispara un manejador:
- `modo_disparo = "Evento"`: Se dispara cuando ocurre un tipo de evento especifico (requiere `tipo_evento_id`)
- `modo_disparo = "Programado"`: Se dispara segun una expresion de intervalo (requiere `expresion`)

**Relacion**: Un tipo de evento puede tener multiples manejadores (1:N a traves de disparadores). Un manejador puede reaccionar a multiples tipos de evento (1:N). El disparador es la tabla intermedia que establece estas relaciones.

### Registrar un Tipo de Evento

```csharp
// IRegistroTiposEvento se obtiene via ServiceProvider (inyeccion de dependencias)
IRegistroTiposEvento registroTipos = /* desde DI */;

await registroTipos.RegistrarTipoEventoAsync(new TipoEvento
{
    Codigo = "ORDEN_CREADA",
    Nombre = "Orden Creada",
    Descripcion = "Se emite cuando se crea una nueva orden",
    Activo = true,
    CreadoEn = DateTime.Now
});
```

### Registrar un Manejador de Eventos

Un manejador vincula un tipo de evento con un comando a ejecutar:

```csharp
// IRegistroManejadores se obtiene via ServiceProvider (inyeccion de dependencias)
IRegistroManejadores registroManejadores = /* desde DI */;

var manejadorId = await registroManejadores.RegistrarManejadorAsync(new ManejadorEvento
{
    Codigo = "NOTIFICAR_ORDEN_CREADA",
    Nombre = "Notificar orden creada",
    Descripcion = "Envia notificacion al crear una orden",
    RutaComando = "notificacion enviar",
    ArgumentosComando = "--tipo=email",
    Activo = true,
    CreadoEn = DateTime.Now
});
```

### Configurar el Disparador

Vincula el manejador con el tipo de evento:

```csharp
var tipoEvento = await registroTipos.ObtenerTipoEventoPorCodigoAsync("ORDEN_CREADA");

await registroManejadores.RegistrarDisparadorAsync(new DisparadorManejador
{
    ManejadorEventoId = manejadorId,
    Nombre = "Disparador Orden Creada",
    TipoEventoId = tipoEvento.Id,
    ModoDisparo = "Evento",
    Activo = true,
    Prioridad = 1,
    CreadoEn = DateTime.Now
});
```

### Publicar un Evento

Para publicar un evento, utiliza `IRegistroEventoBuilder`. Este builder permite registrar eventos en la cola de manera fluida y tipada:

```csharp
public class OrdenServicio
{
    private readonly IRegistroEventoBuilder _registroEventoBuilder;

    public OrdenServicio(IRegistroEventoBuilder registroEventoBuilder)
    {
        _registroEventoBuilder = registroEventoBuilder;
    }

    public async Task CrearOrdenAsync(Orden orden)
    {
        // Guardar orden...

        await _registroEventoBuilder
            .Argumentos("ORDEN_CREADA", orden, orden.Id)
            .RegistrarEnColaAsync();
    }
}
```

Cuando el `ServicioProcesadorEventos` procesa el evento, agrega automaticamente los parametros `--origen=evento`, `--codigo={tipoEvento}` y opcionalmente `--agregado-id={valor}` al comando. Estos parametros se antepone a los argumentos configurados en el manejador.

El comando puede recibir estos parametros mediante la clase de parametros:

```csharp
public class NotificarOrdenComando : ComandoBase<string, ResultadoComando>
{
    private NotificarParametros _parametros;

    public override void Preparar(
        ICollection<Parametro> parametros,
        IConfiguracion configuracion,
        ILogger logger)
    {
        _parametros = Parametro.New<NotificarParametros>(parametros);
    }

    public override async Task EjecutarAsync(
        IStream<string, ResultadoComando> stream,
        CancellationToken token = default)
    {
        // _parametros.Origen indica si viene de "evento" o "disparador"
        // _parametros.Codigo contiene el codigo del evento o disparador
        // _parametros.AgregadoId contiene el ID del agregado (si aplica)
        var datos = stream.ObtenerEntrada();

        if (_parametros.Origen == "evento")
        {
            stream.Escribir(ResultadoComando.Exito(
                $"Notificacion por evento {_parametros.Codigo} para agregado {_parametros.AgregadoId}"));
        }
    }
}

public class NotificarParametros : IParametro
{
    [Nombre("origen")]
    public string Origen { get; set; } = string.Empty;
    
    [Nombre("codigo")]
    public string Codigo { get; set; } = string.Empty;
    
    [Nombre("agregado-id")]
    public long? AgregadoId { get; set; }
}
```

---

## Tareas Programadas

Las tareas programadas permiten ejecutar comandos en intervalos definidos.

El sistema rastrea la ultima ejecucion de cada tarea mediante el campo `ultima_ejecucion` en `per_disparadores_manejador`. Al evaluar si una tarea debe ejecutarse, calcula la proxima ejecucion sumando el intervalo a `ultima_ejecucion`. Si no existe una ejecucion previa, la tarea se ejecuta inmediatamente.

### Registrar una Tarea Programada

```csharp
// IRegistroManejadores se obtiene via ServiceProvider (inyeccion de dependencias)
IRegistroManejadores registroManejadores = /* desde DI */;

var manejadorId = await registroManejadores.RegistrarManejadorAsync(new ManejadorEvento
{
    Codigo = "LIMPIAR_LOGS",
    Nombre = "Limpieza de logs",
    RutaComando = "sistema limpiar-logs",
    Activo = true,
    CreadoEn = DateTime.Now
});

await registroManejadores.RegistrarDisparadorAsync(new DisparadorManejador
{
    ManejadorEventoId = manejadorId,
    Nombre = "Disparador Limpieza Logs",
    ModoDisparo = "Programado",
    Expresion = "00:01:00:00",
    Activo = true,
    Prioridad = 1,
    CreadoEn = DateTime.Now
});
```

**Parametros en Tareas Programadas**: Al igual que los eventos, las tareas programadas tambien reciben parametros automaticos:
- `--origen=disparador`
- `--codigo={codigoDelDisparador}`

Esto permite que el mismo comando pueda distinguir si se ejecuta por un evento o por una tarea programada.

### Expresiones de Intervalo

Formato: `dd:hh:mm:ss` (dias:horas:minutos:segundos)

| Expresion | Frecuencia |
|-----------|------------|
| `00:00:01:00` | Cada minuto |
| `00:00:30:00` | Cada 30 minutos |
| `00:01:00:00` | Cada hora |
| `00:06:00:00` | Cada 6 horas |
| `01:00:00:00` | Cada dia |
| `07:00:00:00` | Cada semana |

---

## Esquema de Base de Datos

Los esquemas se inicializan automaticamente con `InicializarLineaComandoAsync()`.

### Tablas de Cola de Comandos

- `per_comandos_registrados`: Catalogo de comandos disponibles
- `per_cola_comandos`: Comandos encolados pendientes de ejecucion

### Tablas de Event-Driven

- `per_tipos_evento`: Catalogo de tipos de eventos
- `per_manejadores_evento`: Manejadores que responden a eventos
- `per_disparadores_manejador`: Configuracion de cuando se dispara cada manejador
- `per_eventos_outbox`: Eventos publicados pendientes de procesar

> **Nota**: El esquema de base de datos es compatible con PostgreSQL, SQL Server y SQLite. Los tipos de datos se adaptan automaticamente segun el motor de base de datos seleccionado.

---

## Comportamiento en Conflictos (Insert-Only)

Las clases de registro utilizan una estrategia **insert-only**. Esto significa que:

- Si el registro **no existe**: Se inserta con los valores proporcionados por el codigo
- Si el registro **ya existe**: Se retorna el ID existente **sin modificar ningun campo**

### Prioridad a la Base de Datos

**La base de datos tiene prioridad sobre el codigo.** Una vez que un registro existe, sus valores no se sobrescriben automaticamente. Esto permite:

- Modificar configuraciones directamente en base de datos sin que el codigo las restaure
- Mantener consistencia entre lo definido en BD y el comportamiento real del sistema
- Evitar confusiones sobre que valores estan activos

### Tablas Afectadas

| Tabla | Clave Unica | Comportamiento en Conflicto |
|-------|-------------|----------------------------|
| `per_comandos_registrados` | `ruta_comando` | No actualiza nada, retorna ID existente |
| `per_tipos_evento` | `codigo` | No actualiza nada, retorna ID existente |
| `per_manejadores_evento` | `codigo` | No actualiza nada, retorna ID existente |
| `per_disparadores_manejador` | `nombre` | No actualiza nada, retorna ID existente |

---

## Arquitectura

```
                    +-------------------+
                    |   Tu Aplicación   |
                    +-------------------+
                           |
          +----------------+----------------+
          |                                 |
          v                                 v
+----------------------+         +-------------------+
| IAlmacenColaComandos |         | IRegistroEventoBuilder |
| (Encolar comandos)   |         | (Publicar eventos)|
+----------------------+         +-------------------+
          |                                 |
          v                                 v
+-------------------+            +-------------------+
| per_cola_comandos |            | per_eventos_outbox|
| (Tabla de BD)     |            | (Tabla de BD)     |
+-------------------+            +-------------------+
          ^                                 |
          |                                 v
          |                    +-------------------------+
          |                    |ServicioProcesadorEventos|
          |                    | (BackgroundService)     |
          |                    +-------------------------+
          |                                 |
          |                 Encola comandos segun manejadores
          |                                 |
          +<--------------------------------+
          |
          v
+-------------------+
|ServicioColaComandos|
| (BackgroundService)|
+-------------------+
          |
          v
+-------------------+
| Ejecuta comandos  |
| en paralelo       |
+-------------------+
```

---
