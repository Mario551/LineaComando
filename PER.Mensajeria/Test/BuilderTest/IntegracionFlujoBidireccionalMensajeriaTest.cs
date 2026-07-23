using System.Threading.Channels;
using BuilderTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Datos.Configuracion;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace BuilderTest;

public class IntegracionFlujoBidireccionalMensajeriaTest
{
    public static IEnumerable<object[]> Motores
    {
        get
        {
            yield return new object[] { MotorFlujoBidireccionalPrueba.PostgreSql };
            yield return new object[] { MotorFlujoBidireccionalPrueba.SqlServer };
        }
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task FlujoCompleto_DebeRecibirProcesarEnviarYConfirmar(
        MotorFlujoBidireccionalPrueba motor)
    {
        ConfiguracionBaseDatosPrueba baseDatos = await CrearBaseDatosAsync(motor);
        ComunicacionMensajeriaPrueba comunicacion = new();
        RegistroLoggerPrueba registroLogger = new();
        ServiceCollection servicios = new();
        servicios.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new LoggerProviderPrueba(registroLogger));
        });
        servicios.AddSingleton(comunicacion);
        servicios.AddScoped<IContextoConversacionServicio, ContextoConversacionPrueba>();
        servicios.AgregarMensajeria(builder =>
        {
            ConfigurarBaseDatos(builder, baseDatos);
            builder
                .AgregarWorkerOrquestador()
                .AgregarWorkerMensajeria<ComunicacionMensajeriaPrueba>();
        });
        ReconfigurarContextoPorEsquema(servicios, baseDatos);

        await using ServiceProvider proveedor = servicios.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await CrearCuentaCanalAsync(proveedor, baseDatos.Cuenta);
        List<IHostedService> hostedServices = proveedor.GetServices<IHostedService>().ToList();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        try
        {
            foreach (IHostedService hostedService in hostedServices)
            {
                await hostedService.StartAsync(timeout.Token);
            }

            DTORegistrarMensajeEntranteSolicitud solicitud = CrearSolicitud(baseDatos.Cuenta);
            await comunicacion.PublicarEntradaAsync(solicitud, timeout.Token);
            DTOEnvioMensajePendiente mensajeEnviado =
                await comunicacion.MensajeEnviado.Task.WaitAsync(timeout.Token);
            await EsperarConfirmacionAsync(
                proveedor,
                mensajeEnviado.IDEnvioMensaje,
                timeout.Token);

            using IServiceScope alcance = proveedor.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider
                .GetRequiredService<MensajeriaContextoDB>();
            DAOEnvioMensaje envio = await contexto.EnviosMensaje.AsNoTracking().SingleAsync(
                envioActual => envioActual.ID == mensajeEnviado.IDEnvioMensaje,
                timeout.Token);
            DAOProcesamientoInternoMensaje procesamiento = await contexto
                .ProcesamientosInternosMensaje
                .AsNoTracking()
                .SingleAsync(timeout.Token);

            Assert.Equal("enviado", envio.IDEstadoEnvioMensaje);
            Assert.Equal(1, envio.Intentos);
            Assert.NotNull(envio.FechaEnviado);
            Assert.Equal("procesado", procesamiento.IDEstadoProcesamientoInternoMensaje);
            Assert.Equal("respuesta bidireccional", mensajeEnviado.Mensaje.Contenido);
            Assert.Equal("whatsapp", mensajeEnviado.Canal);
            Assert.Equal(baseDatos.Cuenta, mensajeEnviado.Cuenta);
            registroLogger.AssertSinErrores();
        }
        finally
        {
            using CancellationTokenSource timeoutApagado = new(TimeSpan.FromSeconds(10));

            for (int indice = hostedServices.Count - 1; indice >= 0; indice--)
            {
                await hostedServices[indice].StopAsync(timeoutApagado.Token);
            }
        }
    }

    private static async Task<ConfiguracionBaseDatosPrueba> CrearBaseDatosAsync(
        MotorFlujoBidireccionalPrueba motor)
    {
        string cuenta = $"cuenta_bidireccional_{Guid.NewGuid():N}";

        if (motor == MotorFlujoBidireccionalPrueba.PostgreSql)
        {
            string connectionString = LeerVariableObligatoria(
                "MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL");
            string esquema = $"test_mensajeria_bidireccional_{Guid.NewGuid():N}";
            await new InicializadorEsquemaMensajeriaPostgres(
                connectionString,
                esquema)
                .InicializarAsync();
            return new ConfiguracionBaseDatosPrueba(
                motor,
                connectionString,
                esquema,
                cuenta);
        }

        string connectionStringSqlServer = LeerVariableObligatoria(
            "MENSAJERIA_COMANDOS_CONEXION_SQLSERVER");
        string esquemaSqlServer = $"test_mensajeria_bidireccional_sql_{Guid.NewGuid():N}";
        await new InicializadorEsquemaMensajeriaSqlServer(
            connectionStringSqlServer,
            esquemaSqlServer)
            .InicializarAsync();
        return new ConfiguracionBaseDatosPrueba(
            motor,
            connectionStringSqlServer,
            esquemaSqlServer,
            cuenta);
    }

    private static void ConfigurarBaseDatos(
        IMensajeriaBuilder builder,
        ConfiguracionBaseDatosPrueba baseDatos)
    {
        if (baseDatos.Motor == MotorFlujoBidireccionalPrueba.PostgreSql)
        {
            builder.UsarPostgreSQL(baseDatos.ConnectionString, baseDatos.Esquema);
            return;
        }

        builder.UsarSqlServer(baseDatos.ConnectionString, baseDatos.Esquema);
    }

    private static void ReconfigurarContextoPorEsquema(
        IServiceCollection servicios,
        ConfiguracionBaseDatosPrueba baseDatos)
    {
        for (int indice = servicios.Count - 1; indice >= 0; indice--)
        {
            Type tipoServicio = servicios[indice].ServiceType;

            if (tipoServicio == typeof(MensajeriaContextoDB)
                || tipoServicio == typeof(DbContextOptions<MensajeriaContextoDB>)
                || tipoServicio == typeof(DbContextOptions))
            {
                servicios.RemoveAt(indice);
            }
        }

        servicios.AddDbContext<MensajeriaContextoDB>(opciones =>
        {
            opciones.ReplaceService<IModelCacheKeyFactory, ModeloCachePorContextoPrueba>();

            if (baseDatos.Motor == MotorFlujoBidireccionalPrueba.PostgreSql)
            {
                NpgsqlConnectionStringBuilder builder = new(baseDatos.ConnectionString)
                {
                    SearchPath = baseDatos.Esquema
                };
                opciones.UseNpgsql(builder.ConnectionString);
                return;
            }

            opciones.UseSqlServer(baseDatos.ConnectionString);
        });
    }

    private static async Task CrearCuentaCanalAsync(
        IServiceProvider proveedor,
        string cuenta)
    {
        using IServiceScope alcance = proveedor.CreateScope();
        MensajeriaContextoDB contexto = alcance.ServiceProvider
            .GetRequiredService<MensajeriaContextoDB>();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(
            canalActual => canalActual.Canal == "whatsapp");
        contexto.CuentasCanal.Add(new DAOCuentaCanal
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = cuenta,
            Activa = true
        });
        await contexto.SaveChangesAsync();
    }

    private static DTORegistrarMensajeEntranteSolicitud CrearSolicitud(string cuenta)
    {
        return new DTORegistrarMensajeEntranteSolicitud
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = "whatsapp",
                Cuenta = cuenta,
                IdentificadorParticipante = "3001234567",
                TipoParticipante = "telefono",
                TipoMensaje = "texto",
                TelefonoOrigen = "3001234567",
                TelefonoDestino = "6011234567",
                Contenido = "mensaje bidireccional",
                IdentificadorExternoMensaje = Guid.NewGuid().ToString("N"),
                FechaMensaje = DateTime.Now
            }
        };
    }

    private static async Task EsperarConfirmacionAsync(
        IServiceProvider proveedor,
        long idEnvioMensaje,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using IServiceScope alcance = proveedor.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider
                .GetRequiredService<MensajeriaContextoDB>();
            string? estado = await contexto.EnviosMensaje
                .AsNoTracking()
                .Where(envio => envio.ID == idEnvioMensaje)
                .Select(envio => envio.IDEstadoEnvioMensaje)
                .SingleOrDefaultAsync(cancellationToken);

            if (estado == "enviado")
            {
                return;
            }

            if (estado == "fallido")
            {
                throw new InvalidOperationException(
                    $"El envio {idEnvioMensaje} termino fallido.");
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

    public enum MotorFlujoBidireccionalPrueba
    {
        PostgreSql,
        SqlServer
    }

    private sealed record ConfiguracionBaseDatosPrueba(
        MotorFlujoBidireccionalPrueba Motor,
        string ConnectionString,
        string Esquema,
        string Cuenta);

    private sealed class ContextoConversacionPrueba : IContextoConversacionServicio
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
                        Contenido = "respuesta bidireccional",
                        FechaMensaje = DateTime.Now
                    }
                ]
            });
        }
    }

    private sealed class ComunicacionMensajeriaPrueba : IComunicacionMensajeriaAPI
    {
        private readonly Channel<DTORegistrarMensajeEntranteSolicitud> entradas =
            Channel.CreateUnbounded<DTORegistrarMensajeEntranteSolicitud>();

        public TaskCompletionSource<DTOEnvioMensajePendiente> MensajeEnviado { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask PublicarEntradaAsync(
            DTORegistrarMensajeEntranteSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            return entradas.Writer.WriteAsync(solicitud, cancellationToken);
        }

        public async Task<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
            CancellationToken cancellationToken)
        {
            return await entradas.Reader.ReadAsync(cancellationToken);
        }

        public Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
            DTOEnvioMensajePendiente mensaje,
            CancellationToken cancellationToken)
        {
            MensajeEnviado.TrySetResult(mensaje);
            return Task.FromResult(new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = mensaje.IDEnvioMensaje,
                Estado = "enviado"
            });
        }
    }

    private sealed class ModeloCachePorContextoPrueba : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            return (context.GetType(), context.ContextId.InstanceId, designTime);
        }
    }
}
