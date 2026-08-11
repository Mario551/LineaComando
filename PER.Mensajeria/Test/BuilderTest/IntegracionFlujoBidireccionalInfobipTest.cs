using System.Net;
using System.Text;
using System.Text.Json;
using BuilderTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Datos.Infobip.Esquema;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace BuilderTest;

public class IntegracionFlujoBidireccionalInfobipTest
{
    public static IEnumerable<object[]> Motores
    {
        get
        {
            yield return new object[] { MotorInfobipPrueba.PostgreSql };
            yield return new object[] { MotorInfobipPrueba.SqlServer };
        }
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task WebhookContextoYEnvio_DebePersistirIntentoAceptado(
        MotorInfobipPrueba motor)
    {
        ConfiguracionBaseDatosInfobipPrueba baseDatos =
            await CrearBaseDatosAsync(motor);
        HttpInfobipPrueba handler = new();
        RegistroLoggerPrueba registroLogger = new();
        ServiceCollection servicios = new();
        servicios.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new LoggerProviderPrueba(registroLogger));
        });
        servicios.AddScoped<IContextoConversacionServicio,
            ContextoConversacionInfobipPrueba>();
        servicios.AgregarMensajeria(builder =>
        {
            ConfigurarBaseDatos(builder, baseDatos);
            builder
                .AgregarWorkerOrquestador()
                .AgregarWorkerMensajeriaInfobip(
                    new Uri("https://api.infobip.test"),
                    "api-key-prueba");
        });
        servicios
            .AddHttpClient<IInfobipWhatsAppCliente, InfobipWhatsAppCliente>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        ReconfigurarContextoPorEsquema(servicios, baseDatos);

        await using ServiceProvider proveedor = servicios.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await CrearCuentaCanalAsync(proveedor, baseDatos.Cuenta);
        List<IHostedService> hostedServices = proveedor
            .GetServices<IHostedService>()
            .ToList();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(40));

        try
        {
            foreach (IHostedService hostedService in hostedServices)
            {
                await hostedService.StartAsync(timeout.Token);
            }

            IRecepcionWebhookInfobipAPI receptor = proveedor
                .GetRequiredService<IRecepcionWebhookInfobipAPI>();
            DTOResultadoRecepcionWebhookInfobip resultadoRecepcion =
                await receptor.RecibirAsync(
                    CrearWebhook(baseDatos.Cuenta),
                    timeout.Token);
            PeticionHttpInfobipPrueba peticion = await handler
                .PeticionRecibida
                .Task
                .WaitAsync(timeout.Token);
            EstadoPersistidoPrueba estado = await EsperarEstadoFinalAsync(
                proveedor,
                timeout.Token);

            DTOResultadoRecepcionMensajeInfobip recepcion = Assert.Single(
                resultadoRecepcion.Resultados);
            Assert.True(recepcion.Registrado);
            Assert.Equal("procesado", estado.EstadoProcesamiento);
            Assert.Equal("enviado", estado.EstadoEnvio);
            Assert.Equal(1, estado.IntentosEnvio);
            Assert.Equal("aceptado", estado.IntentoInfobip.IDEstado);
            Assert.Equal(1, estado.IntentoInfobip.NumeroIntento);
            Assert.Equal("infobip-integracion-1", estado.IntentoInfobip.MessageIDInfobip);
            Assert.Equal("/whatsapp/1/message/text", peticion.Ruta);
            Assert.Equal("App", peticion.EsquemaAutorizacion);
            Assert.Equal("api-key-prueba", peticion.CredencialAutorizacion);

            using JsonDocument cuerpo = JsonDocument.Parse(peticion.Cuerpo);
            JsonElement raiz = cuerpo.RootElement;
            Assert.Equal(baseDatos.Cuenta, raiz.GetProperty("from").GetString());
            Assert.Equal("573163432479", raiz.GetProperty("to").GetString());
            Assert.Equal(
                "respuesta desde contexto",
                raiz.GetProperty("content").GetProperty("text").GetString());
            Assert.Equal(
                estado.IDEnvioMensaje.ToString(),
                raiz.GetProperty("callbackData").GetString());
            registroLogger.AssertSinErrores();
        }
        finally
        {
            using CancellationTokenSource apagado = new(TimeSpan.FromSeconds(10));

            for (int indice = hostedServices.Count - 1; indice >= 0; indice--)
            {
                await hostedServices[indice].StopAsync(apagado.Token);
            }
        }
    }

    private static async Task<ConfiguracionBaseDatosInfobipPrueba>
        CrearBaseDatosAsync(MotorInfobipPrueba motor)
    {
        string cuenta = $"cuenta_infobip_{Guid.NewGuid():N}";
        string variable = motor == MotorInfobipPrueba.PostgreSql
            ? "MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL"
            : "MENSAJERIA_COMANDOS_CONEXION_SQLSERVER";
        string conexion = LeerVariableObligatoria(variable);
        string esquema = motor == MotorInfobipPrueba.PostgreSql
            ? $"test_mensajeria_infobip_{Guid.NewGuid():N}"
            : $"test_mensajeria_infobip_sql_{Guid.NewGuid():N}";

        if (motor == MotorInfobipPrueba.PostgreSql)
        {
            await new InicializadorEsquemaMensajeriaPostgres(conexion, esquema)
                .InicializarAsync();
        }
        else
        {
            await new InicializadorEsquemaMensajeriaSqlServer(conexion, esquema)
                .InicializarAsync();
        }

        InicializadorModuloEsquemaInfobip inicializadorInfobip = new();
        await inicializadorInfobip.InicializarAsync(
            new ConfiguracionInicializacionEsquemaMensajeria
            {
                Proveedor = motor == MotorInfobipPrueba.PostgreSql
                    ? ProveedorBaseDatosMensajeria.PostgreSql
                    : ProveedorBaseDatosMensajeria.SqlServer,
                CadenaConexion = conexion,
                Esquema = esquema
            },
            CancellationToken.None);

        return new ConfiguracionBaseDatosInfobipPrueba(
            motor,
            conexion,
            esquema,
            cuenta);
    }

    private static void ConfigurarBaseDatos(
        IMensajeriaBuilder builder,
        ConfiguracionBaseDatosInfobipPrueba baseDatos)
    {
        if (baseDatos.Motor == MotorInfobipPrueba.PostgreSql)
        {
            builder.UsarPostgreSQL(baseDatos.Conexion, baseDatos.Esquema);
            return;
        }

        builder.UsarSqlServer(baseDatos.Conexion, baseDatos.Esquema);
    }

    private static void ReconfigurarContextoPorEsquema(
        IServiceCollection servicios,
        ConfiguracionBaseDatosInfobipPrueba baseDatos)
    {
        for (int indice = servicios.Count - 1; indice >= 0; indice--)
        {
            Type tipo = servicios[indice].ServiceType;

            if (tipo == typeof(MensajeriaContextoDB)
                || tipo == typeof(DbContextOptions<MensajeriaContextoDB>)
                || tipo == typeof(DbContextOptions))
            {
                servicios.RemoveAt(indice);
            }
        }

        servicios.AddDbContext<MensajeriaContextoDB>(opciones =>
        {
            opciones.ReplaceService<IModelCacheKeyFactory,
                ModeloCacheInfobipPrueba>();

            if (baseDatos.Motor == MotorInfobipPrueba.PostgreSql)
            {
                NpgsqlConnectionStringBuilder builder = new(baseDatos.Conexion)
                {
                    SearchPath = baseDatos.Esquema
                };
                opciones.UseNpgsql(builder.ConnectionString);
                return;
            }

            opciones.UseSqlServer(baseDatos.Conexion);
        });
    }

    private static async Task CrearCuentaCanalAsync(
        IServiceProvider proveedor,
        string cuenta)
    {
        using IServiceScope alcance = proveedor.CreateScope();
        MensajeriaContextoDB contexto = alcance.ServiceProvider
            .GetRequiredService<MensajeriaContextoDB>();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion
            .SingleAsync(actual => actual.Canal == "whatsapp");
        contexto.CuentasCanal.Add(new DAOCuentaCanal
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = cuenta,
            Activa = true
        });
        await contexto.SaveChangesAsync();
    }

    private static DTOInfobipWebhook CrearWebhook(string cuenta)
    {
        return new DTOInfobipWebhook
        {
            MessageCount = 1,
            PendingMessageCount = 0,
            Results =
            [
                new DTOInfobipResult
                {
                    From = "573163432479",
                    To = cuenta,
                    IntegrationType = "WHATSAPP",
                    ReceivedAt = "2026-08-07T12:00:00.000+0000",
                    MessageId = $"infobip-entrada-{Guid.NewGuid():N}",
                    Message = new DTOInfobipMessage
                    {
                        Type = "TEXT",
                        Text = "mensaje entrante de integracion"
                    },
                    Contact = new DTOInfobipContactProfile
                    {
                        Name = "Mario",
                        PhoneNumber = "573163432479",
                        UserId = "CO.1776622120445919"
                    },
                    Price = new DTOInfobipMessagePrice
                    {
                        PricePerMessage = 0,
                        Currency = "USD"
                    }
                }
            ]
        };
    }

    private static async Task<EstadoPersistidoPrueba> EsperarEstadoFinalAsync(
        IServiceProvider proveedor,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using IServiceScope alcance = proveedor.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider
                .GetRequiredService<MensajeriaContextoDB>();
            DAOEnvioMensaje? envio = await contexto.EnviosMensaje
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (envio is not null && envio.IDEstadoEnvioMensaje == "enviado")
            {
                DAOIntentoEnvioMensajeInfobip intento = await contexto
                    .IntentosEnvioMensajeInfobip
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);
                string procesamiento = await contexto
                    .ProcesamientosInternosMensaje
                    .AsNoTracking()
                    .Select(actual => actual.IDEstadoProcesamientoInternoMensaje)
                    .SingleAsync(cancellationToken);
                return new EstadoPersistidoPrueba(
                    envio.ID,
                    envio.IDEstadoEnvioMensaje,
                    envio.Intentos,
                    procesamiento,
                    intento);
            }

            if (envio?.IDEstadoEnvioMensaje == "fallido")
            {
                throw new InvalidOperationException(
                    $"El envío {envio.ID} terminó fallido: {envio.Error}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static string LeerVariableObligatoria(string nombre)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        Assert.False(
            string.IsNullOrWhiteSpace(valor),
            $"La variable de entorno {nombre} es obligatoria.");
        return valor!;
    }

    public enum MotorInfobipPrueba
    {
        PostgreSql,
        SqlServer
    }

    private sealed record ConfiguracionBaseDatosInfobipPrueba(
        MotorInfobipPrueba Motor,
        string Conexion,
        string Esquema,
        string Cuenta);

    private sealed record EstadoPersistidoPrueba(
        long IDEnvioMensaje,
        string EstadoEnvio,
        int IntentosEnvio,
        string EstadoProcesamiento,
        DAOIntentoEnvioMensajeInfobip IntentoInfobip);

    private sealed class ContextoConversacionInfobipPrueba :
        IContextoConversacionServicio
    {
        public Task<ResultadoContextoConversacion> ResolverAsync(
            SolicitudContextoConversacion solicitud,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ResultadoContextoConversacion
            {
                TipoResultado = ResultadoContextoConversacionTipo.ConSalidas,
                MensajesSalientes =
                [
                    new MensajeSalienteContexto
                    {
                        TipoMensaje = "texto",
                        TelefonoOrigen = solicitud.TelefonoDestino,
                        TelefonoDestino = solicitud.TelefonoOrigen,
                        Contenido = "respuesta desde contexto",
                        FechaMensaje = DateTime.Now
                    }
                ]
            });
        }
    }

    private sealed class HttpInfobipPrueba : HttpMessageHandler
    {
        public TaskCompletionSource<PeticionHttpInfobipPrueba> PeticionRecibida
            { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string cuerpo = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            PeticionRecibida.TrySetResult(new PeticionHttpInfobipPrueba(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                cuerpo));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "to": "573163432479",
                      "messageCount": 1,
                      "messageId": "infobip-integracion-1",
                      "status": {
                        "groupId": 1,
                        "groupName": "PENDING",
                        "id": 7,
                        "name": "PENDING_ENROUTE",
                        "description": "Message sent to next instance"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed record PeticionHttpInfobipPrueba(
        string Ruta,
        string? EsquemaAutorizacion,
        string? CredencialAutorizacion,
        string Cuerpo);

    private sealed class ModeloCacheInfobipPrueba : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            return (context.GetType(), context.ContextId.InstanceId, designTime);
        }
    }
}
