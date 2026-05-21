# PER.Comandos.LineaComandos

Sistema de comandos para aplicaciones .NET con cola asincrona, procesamiento event-driven, tareas programadas y resultados durables.

El sistema usa base de datos como fuente de verdad y colas en memoria para activar el procesamiento dentro del proceso actual.

## Dependencias

- .NET 8.0+
- PostgreSQL con Npgsql o SQL Server con Microsoft.Data.SqlClient
- Dapper
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection

## Configuracion Con El Builder

### ASP.NET Core

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLineaComando(async (sp, builderInicializador, token) =>
    {
        IBuilderComando builderComando = builderInicializador.NewBuilderComando();

        IBuilderManejador builderManejador = await builderComando
            .Argumentos("orden pagar", "Procesa el pago de una orden")
            .Accion(new PagarOrdenComando())
            .Resultado(new PagoOrdenResultadoProcesador())
            .RegistrarAsync();

        IBuilderTipoEvento builderTipoEvento = builderInicializador.NewBuilderTipoEvento();
        ITipoEvento tipoEvento = await builderTipoEvento
            .Argumentos("ORDEN_CREADA", "Orden creada", "Se emite cuando se crea una orden")
            .RegistrarAsync();

        IBuilderDisparador builderDisparador = await builderManejador
            .Argumentos(
                "NOTIFICAR_ORDEN_CREADA",
                "Notificar orden creada",
                string.Empty,
                "Envia notificacion al crear una orden")
            .RegistrarAsync();

        await builderDisparador
            .Argumentos("DISPARADOR_ORDEN_CREADA", 1, tipoEvento)
            .RegistrarAsync();
    })
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")!, "linea_comando")
    .SetRutaResultadosComandos("/var/app/resultados-comandos")
    .SetMaxParalelismoCola(4)
    .Build();

var app = builder.Build();

await app.Services.InicializarLineaComandoAsync();

app.Run();
```

### Aplicacion De Consola Con Host

```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services
            .AddLineaComando(async (sp, builderInicializador, token) =>
            {
                await builderInicializador
                    .NewBuilderComando()
                    .Argumentos("orden pagar", "Procesa el pago de una orden")
                    .Accion(new PagarOrdenComando())
                    .RegistrarAsync();
            })
            .UsePostgresql(context.Configuration.GetConnectionString("Default")!, "linea_comando")
            .SetMaxParalelismoCola(4)
            .Build();
    })
    .Build();

await host.Services.InicializarLineaComandoAsync();
await host.RunAsync();
```

## Base De Datos Y Esquemas

El soporte operativo documentado es:

- `.UsePostgresql(connectionString)` o `.UsePostgresql(connectionString, esquema)`
- `.UseSqlServer(connectionString)` o `.UseSqlServer(connectionString, esquema)`
- `.SetEsquemaBaseDatos(esquema)` si se quiere configurar el esquema despues de elegir motor

Esquemas predeterminados:

| Motor | Esquema predeterminado |
|-------|------------------------|
| PostgreSQL | `public` |
| SQL Server | `dbo` |

`InicializarLineaComandoAsync()` crea las tablas, funciones y procedimientos necesarios para el esquema configurado.

## Parametros Del Builder

| Metodo | Descripcion | Valor por defecto |
|--------|-------------|-------------------|
| `SetMaxParalelismoCola(int)` | Maximo de comandos ejecutandose en paralelo | `4` |
| `SetRutaResultadosComandos(string)` | Ruta base para payloads de resultado mayores a 256KB | `null` |

## Servicios En Segundo Plano

`Build()` registra estos servicios:

| Servicio | Responsabilidad |
|----------|-----------------|
| `ServicioColaComandos` | Inicia `ProcesadorColaComandos`, carga comandos pendientes desde BD y consume `IColaComandosMemoria` |
| `ServicioProcesadorEventos` | Carga eventos pendientes desde BD, consume `IColaEventosMemoria` y encola comandos asociados |
| `ServicioTareasProgramadas` | Inicia el planificador de disparadores programados |

## Comandos

### Crear Un Comando

Un comando hereda de `ComandoBase<TRead, TWrite>`. La ejecucion retorna el resultado directamente.

```csharp
public sealed class PagarOrdenComando : ComandoBase<string, ResultadoComando>
{
    private PagarOrdenParametros _parametros = new();

    public override void Preparar(ICollection<Parametro> parametros)
    {
        _parametros = Parametro.New<PagarOrdenParametros>(parametros);
    }

    public override Task<ResultadoComando> EjecutarAsync(
        string entrada,
        CancellationToken token = default)
    {
        PagoOrden datos = JsonSerializer.Deserialize<PagoOrden>(entrada)!;

        object salida = new
        {
            datos.OrdenId,
            Estado = "pagada"
        };

        return Task.FromResult(ResultadoComando.Exito(salida));
    }
}

public sealed class PagarOrdenParametros : IParametro
{
    [Nombre("ordenId")]
    public long OrdenId { get; set; }

    [Nombre("monto")]
    public decimal Monto { get; set; }
}
```

### Registrar Un Comando

```csharp
await builderInicializador
    .NewBuilderComando()
    .Argumentos("orden pagar", "Procesa el pago de una orden")
    .Accion(new PagarOrdenComando())
    .RegistrarAsync();
```

Tambien se puede registrar directamente con `IRegistroComandos<string, ResultadoComando>`:

```csharp
await registroComandos.RegistrarComandoAsync(
    new MetadatosComando
    {
        RutaComando = "orden pagar",
        Descripcion = "Procesa el pago de una orden"
    },
    new Nodo<string, ResultadoComando>(new PagarOrdenComando()),
    token);
```

## Cola De Comandos En Memoria

La base de datos persiste la solicitud. La cola en memoria activa el worker del proceso actual.

Flujo normal:

```text
IColaComandosMemoria.EncolarAsync(SolicitudComando)
-> IAlmacenColaComandos.EncolarAsync(ComandoEnCola)
-> per_cola_comandos
-> Channel<ComandoEnCola>
-> ProcesadorColaComandos
-> comando.EjecutarAsync(entrada)
-> persistir estado/resultado
-> completar Task local si alguien espera
```

Ejemplo:

```csharp
public sealed class PagoServicio
{
    private readonly IColaComandosMemoria _colaComandos;

    public PagoServicio(IColaComandosMemoria colaComandos)
    {
        _colaComandos = colaComandos;
    }

    public async Task<ResultadoComando> ProcesarPagoAsync(long ordenId, decimal monto)
    {
        ComandoEncolado comando = await _colaComandos.EncolarAsync(new SolicitudComando
        {
            RutaComando = "orden pagar",
            Argumentos = $"--ordenId={ordenId} --monto={monto}",
            DatosDeComando = JsonSerializer.Serialize(new { OrdenId = ordenId, Monto = monto })
        });

        return await comando.Resultado;
    }
}
```

### Recuperar Una Espera

Si el proceso sigue vivo, `ComandoEncolado.Resultado` se completa desde memoria. Si el comando ya termino y la espera fue limpiada, `EsperarComandoAsync` recupera el resultado durable desde BD o archivo.

```csharp
ComandoEncolado comando = await colaComandos.EsperarComandoAsync(comandoId);
ResultadoComando resultado = await comando.Resultado;
```

## Resultados Durables

`ResultadoComando.Salida` es `object?`. Para hacer durable la salida de un comando se registra un procesador de resultado:

```csharp
await builderInicializador
    .NewBuilderComando()
    .Argumentos("orden pagar", "Procesa el pago de una orden")
    .Accion(new PagarOrdenComando())
    .Resultado(new PagoOrdenResultadoProcesador())
    .RegistrarAsync();
```

```csharp
public sealed class PagoOrdenResultadoProcesador : IProcesadorResultadoComando
{
    public string Tipo => "pago_orden";

    public int Version => 1;

    public string Formato => "application/json";

    public Task<string?> SerializarAsync(object? salida, CancellationToken token = default)
    {
        return Task.FromResult<string?>(JsonSerializer.Serialize(salida));
    }

    public Task<object?> DeserializarAsync(string? contenido, CancellationToken token = default)
    {
        PagoOrdenResultado? resultado = JsonSerializer.Deserialize<PagoOrdenResultado>(contenido ?? "{}");
        return Task.FromResult<object?>(resultado);
    }
}
```

Reglas de almacenamiento:

- Si el payload serializado ocupa hasta `256 * 1024` bytes UTF-8, se guarda en `per_cola_comandos_resultados.payload`.
- Si supera ese limite, se guarda en archivo externo y la tabla guarda `ruta_payload`.
- La ruta del archivo sigue el patron `{RutaBase}/{tipo}/v{version}/{comandoId}.{guid}.payload`.
- Para payloads grandes se debe configurar `.SetRutaResultadosComandos(rutaBase)`.

## Event-Driven

### Flujo De Eventos

```text
Tu codigo
-> IRegistroEventoBuilder.Argumentos(...).RegistrarEnColaAsync()
-> per_eventos_outbox
-> IColaEventosMemoria
-> ServicioProcesadorEventos
-> ProcesadorEventos
-> IRegistroManejadores.ObtenerManejadoresParaEventoAsync()
-> IColaComandosMemoria.EncolarAsync(SolicitudComando)
-> ProcesadorColaComandos
```

Al iniciar, `ServicioProcesadorEventos` carga desde BD los eventos pendientes y los encola en memoria.

### Publicar Un Evento

```csharp
public sealed class OrdenServicio
{
    private readonly IRegistroEventoBuilder _registroEventoBuilder;

    public OrdenServicio(IRegistroEventoBuilder registroEventoBuilder)
    {
        _registroEventoBuilder = registroEventoBuilder;
    }

    public async Task CrearOrdenAsync(Orden orden)
    {
        await _registroEventoBuilder
            .Argumentos("ORDEN_CREADA", orden, orden.Id)
            .RegistrarEnColaAsync();
    }
}
```

Cuando el evento encola un comando, se agregan parametros automaticos:

| Parametro | Descripcion |
|-----------|-------------|
| `--origen=evento` | Indica que el comando viene de evento |
| `--codigo={tipoEvento}` | Codigo del tipo de evento |
| `--agregado-id={valor}` | ID del agregado, si existe |

## Tareas Programadas

Las tareas programadas se definen como disparadores con `modo_disparo = "Programado"` y expresion `dd:hh:mm:ss`.

Al iniciar, `ServicioTareasProgramadas` carga los disparadores activos y `CoordinadorTareasProgramadas` programa cada ejecucion en memoria. Cuando una ejecucion vence, se encola el comando en `IColaComandosMemoria` y se actualiza `ultima_ejecucion`.

```csharp
IBuilderDisparador builderDisparador = await builderManejador
    .Argumentos("LIMPIAR_LOGS", "Limpieza de logs", string.Empty, "Limpia logs antiguos")
    .RegistrarAsync();

await builderDisparador
    .Argumentos(
        codigo: "DISPARADOR_LIMPIAR_LOGS",
        prioridad: 1,
        expresion: "00:01:00:00")
    .RegistrarAsync();
```

Ejemplos de expresion:

| Expresion | Frecuencia |
|-----------|------------|
| `00:00:01:00` | Cada minuto |
| `00:01:00:00` | Cada hora |
| `01:00:00:00` | Cada dia |
| `07:00:00:00` | Cada semana |

Los comandos disparados por tareas programadas reciben:

- `--origen=disparador`
- `--codigo={codigoDelDisparador}`

## Esquema De Base De Datos

### Cola De Comandos

| Tabla | Descripcion |
|-------|-------------|
| `per_comandos_registrados` | Catalogo de comandos registrados |
| `per_cola_comandos_estados` | Catalogo de estados validos |
| `per_cola_comandos` | Solicitudes de comandos encoladas |
| `per_cola_comandos_resultados` | Resultado durable y metadata de payload |

Estados oficiales:

- `pendiente`
- `procesando`
- `completado`
- `fallido`

### Event-Driven

| Tabla | Descripcion |
|-------|-------------|
| `per_tipos_evento` | Catalogo de eventos |
| `per_manejadores_evento` | Manejadores asociados a comandos |
| `per_disparadores_manejador` | Disparadores por evento o por programacion |
| `per_eventos_outbox` | Eventos pendientes o procesados |

## Comportamiento En Conflictos

Las clases de registro usan comportamiento insert-only:

- Si el registro no existe, se inserta.
- Si el registro ya existe, se retorna el ID existente sin sobrescribir sus campos.

Esto permite que la base de datos tenga prioridad sobre los valores definidos en codigo.

| Tabla | Clave unica | Comportamiento |
|-------|-------------|----------------|
| `per_comandos_registrados` | `ruta_comando` | Retorna ID existente |
| `per_tipos_evento` | `codigo` | Retorna ID existente |
| `per_manejadores_evento` | `codigo` | Retorna ID existente |
| `per_disparadores_manejador` | `codigo` | Retorna ID existente |

## Arquitectura

```text
Canal externo / aplicacion
        |
        v
IRegistroEventoBuilder o IColaComandosMemoria
        |
        v
Base de datos durable
  - per_eventos_outbox
  - per_cola_comandos
  - per_cola_comandos_resultados
        |
        v
Colas en memoria
  - IColaEventosMemoria
  - IColaComandosMemoria
        |
        v
Servicios en segundo plano
  - ServicioProcesadorEventos
  - ServicioTareasProgramadas
  - ServicioColaComandos
        |
        v
ProcesadorColaComandos
        |
        v
Comando.EjecutarAsync(entrada)
        |
        v
ResultadoComando
        |
        +--> resultado pequeno: per_cola_comandos_resultados.payload
        |
        +--> resultado grande: archivo externo + ruta_payload
        |
        +--> espera local: ComandoEncolado.Resultado
```

La base de datos es la fuente de verdad. Las colas en memoria activan el procesamiento y permiten esperas locales rapidas, pero no reemplazan la persistencia.
