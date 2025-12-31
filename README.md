# PER.Comandos.LineaComandos

Sistema de procesamiento de comandos y eventos para aplicaciones .NET. Incluye cola de comandos asincronos y arquitectura event-driven con outbox pattern.

## Dependencias

- .NET 8.0+
- PostgreSQL (Npgsql)
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
    .AddLineaComando(builder.Configuration.GetConnectionString("Default")!)
    .ConColaComandos(tiempoRefresco: TimeSpan.FromSeconds(2), maxParalelismo: 8)
    .ConEventDriven(
        tiempoRefrescoEventos: TimeSpan.FromSeconds(1),
        tiempoRefrescoTareas: TimeSpan.FromSeconds(5))
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
            .AddLineaComando(context.Configuration.GetConnectionString("Default")!)
            .ConColaComandos()
            .ConEventDriven()
            .Build();
    })
    .Build();

await host.InicializarLineaComandoAsync();
await host.RunAsync();
```

## Servicios en Segundo Plano

Al llamar `Build()`, se registran automaticamente los siguientes `BackgroundService`:

| Servicio | Descripcion |
|----------|-------------|
| `ServicioColaComandos` | Escanea la tabla `cola_comandos`, obtiene comandos pendientes y los ejecuta en paralelo |
| `ServicioProcesadorEventos` | Escanea la tabla `outbox_eventos`, lee eventos pendientes y encola comandos asociados a sus manejadores |
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
var registroComandos = new RegistroComandos<string, ResultadoComando>(connectionString);

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
[Tu Codigo] -> IColaEventos.GuardarEventoAsync() -> [Tabla outbox_eventos]
                                                              |
                                                              v
[ServicioProcesadorEventos] <- ObtenerEventosPendientesAsync()
            |
            v
    ObtenerManejadoresParaEventoAsync()
            |
            v
    Por cada manejador -> IAlmacenColaComandos.EncolarAsync()
            |
            v
[ServicioColaComandos] ejecuta el comando
```

### Modelo de Datos Event-Driven

El sistema event-driven utiliza tres tablas relacionadas:

```
+------------------+       +----------------------+       +------------------------+
|  tipos_evento    |       | manejadores_evento   |       | disparadores_manejador |
+------------------+       +----------------------+       +------------------------+
| id (PK)          |       | id (PK)              |       | id (PK)                |
| codigo           |       | codigo               |       | manejador_evento_id(FK)|---> manejadores_evento
| nombre           |       | nombre               |       | tipo_evento_id (FK)    |---> tipos_evento
| descripcion      |       | descripcion          |       | modo_disparo           |
| activo           |       | ruta_comando         |       | expresion              |
| creado_en        |       | argumentos_comando   |       | activo                 |
+------------------+       | activo               |       | prioridad              |
                           | creado_en            |       | creado_en              |
                           +----------------------+       +------------------------+
```

**tipos_evento**: Catalogo de eventos que pueden ocurrir en tu sistema. Define QUE cosas pueden pasar (ej: "ORDEN_CREADA", "PAGO_RECIBIDO", "USUARIO_REGISTRADO").

**manejadores_evento**: Define QUE COMANDO ejecutar como reaccion. Cada manejador tiene una `ruta_comando` que apunta a un comando registrado en la cola.

**disparadores_manejador**: Es el PUENTE que conecta todo. Define CUANDO se dispara un manejador:
- `modo_disparo = "Evento"`: Se dispara cuando ocurre un tipo de evento especifico (requiere `tipo_evento_id`)
- `modo_disparo = "Programado"`: Se dispara segun una expresion de intervalo (requiere `expresion`)

**Relacion**: Un tipo de evento puede tener multiples manejadores (1:N a traves de disparadores). Un manejador puede reaccionar a multiples tipos de evento (1:N). El disparador es la tabla intermedia que establece estas relaciones.

### Registrar un Tipo de Evento

```csharp
var registroTipos = new RegistroTiposEvento(connectionString);

await registroTipos.RegistrarTipoEventoAsync(new TipoEvento
{
    Codigo = "ORDEN_CREADA",
    Nombre = "Orden Creada",
    Descripcion = "Se emite cuando se crea una nueva orden",
    Activo = true,
    CreadoEn = DateTime.UtcNow
});
```

### Registrar un Manejador de Eventos

Un manejador vincula un tipo de evento con un comando a ejecutar:

```csharp
var registroManejadores = new RegistroManejadores(connectionString);

var manejadorId = await registroManejadores.RegistrarManejadorAsync(new ManejadorEvento
{
    Codigo = "NOTIFICAR_ORDEN_CREADA",
    Nombre = "Notificar orden creada",
    Descripcion = "Envia notificacion al crear una orden",
    RutaComando = "notificacion enviar",
    ArgumentosComando = "--tipo=email",
    Activo = true,
    CreadoEn = DateTime.UtcNow
});
```

### Configurar el Disparador

Vincula el manejador con el tipo de evento:

```csharp
var tipoEvento = await registroTipos.ObtenerTipoEventoPorCodigoAsync("ORDEN_CREADA");

await registroManejadores.RegistrarDisparadorAsync(new DisparadorManejador
{
    ManejadorEventoId = manejadorId,
    TipoEventoId = tipoEvento.Id,
    ModoDisparo = "Evento",
    Activo = true,
    Prioridad = 1,
    CreadoEn = DateTime.UtcNow
});
```

### Publicar un Evento

```csharp
public class OrdenServicio
{
    private readonly IColaEventos _colaEventos;

    public OrdenServicio(IColaEventos colaEventos)
    {
        _colaEventos = colaEventos;
    }

    public async Task CrearOrdenAsync(Orden orden)
    {
        // Guardar orden...

        await _colaEventos.GuardarEventoAsync(new DatosEvento
        {
            TipoEvento = "ORDEN_CREADA",
            AgregadoId = orden.Id,
            Datos = JsonSerializer.Serialize(orden),
            Metadatos = JsonSerializer.Serialize(new { Usuario = "sistema" })
        });
    }
}
```

El `ServicioProcesadorEventos` detectara el evento y encolara el comando `notificacion enviar`.

---

## Tareas Programadas

Las tareas programadas permiten ejecutar comandos en intervalos definidos.

### Registrar una Tarea Programada

```csharp
var registroManejadores = new RegistroManejadores(connectionString);

var manejadorId = await registroManejadores.RegistrarManejadorAsync(new ManejadorEvento
{
    Codigo = "LIMPIAR_LOGS",
    Nombre = "Limpieza de logs",
    RutaComando = "sistema limpiar-logs",
    Activo = true,
    CreadoEn = DateTime.UtcNow
});

await registroManejadores.RegistrarDisparadorAsync(new DisparadorManejador
{
    ManejadorEventoId = manejadorId,
    ModoDisparo = "Programado",
    Expresion = "00:01:00:00",
    Activo = true,
    Prioridad = 1,
    CreadoEn = DateTime.UtcNow
});
```

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

- `comandos_registrados`: Catalogo de comandos disponibles
- `cola_comandos`: Comandos encolados pendientes de ejecucion

### Tablas de Event-Driven

- `tipos_evento`: Catalogo de tipos de eventos
- `manejadores_evento`: Manejadores que responden a eventos
- `disparadores_manejador`: Configuracion de cuando se dispara cada manejador
- `outbox_eventos`: Eventos publicados pendientes de procesar

---

## Arquitectura

```
                    +-------------------+
                    |   Tu Aplicacion   |
                    +-------------------+
                           |
          +----------------+----------------+
          |                                 |
          v                                 v
+-------------------+            +-------------------+
| IAlmacenColaComandos |        |    IColaEventos    |
| (Encolar comandos)|            | (Publicar eventos)|
+-------------------+            +-------------------+
          |                                 |
          v                                 v
+-------------------+            +-------------------+
| cola_comandos     |            | outbox_eventos    |
| (Tabla PostgreSQL)|            | (Tabla PostgreSQL)|
+-------------------+            +-------------------+
          ^                                 |
          |                                 v
          |                      +----------------------+
          |                      |ServicioProcesadorEventos|
          |                      | (BackgroundService)  |
          |                      +----------------------+
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