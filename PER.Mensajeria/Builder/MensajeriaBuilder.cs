using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;
using PER.Mensajeria.Aplicacion.Infobip.CargarPendientes;
using PER.Mensajeria.Aplicacion.Infobip.Cola;
using PER.Mensajeria.Aplicacion.Infobip.ConfirmarMensajeEntrante;
using PER.Mensajeria.Aplicacion.Infobip.Envio;
using PER.Mensajeria.Aplicacion.Infobip.Mapeo;
using PER.Mensajeria.Aplicacion.Infobip.ObtenerMensajeEntrante;
using PER.Mensajeria.Aplicacion.Infobip.RegistrarWebhook;
using PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;
using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Builder.Persistencia;
using PER.Mensajeria.Builder.Worker;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Datos.Infobip.Esquema;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Servicio.Infobip;
using PER.Mensajeria.Servicio.Mensaje;
using PER.Mensajeria.Servicio.Orquestador;

namespace PER.Mensajeria.Builder;

public class MensajeriaBuilder : IMensajeriaBuilder
{
    private readonly IServiceCollection servicios;

    public MensajeriaBuilder(IServiceCollection servicios)
    {
        this.servicios = servicios;
        RegistrarServiciosBase();
    }

    public IMensajeriaBuilder UsarPostgreSQL(string cadenaConexion)
    {
        return UsarPostgreSQL(cadenaConexion, null);
    }

    public IMensajeriaBuilder UsarPostgreSQL(string cadenaConexion, string? esquema)
    {
        string cadenaConexionFinal = ConstruirCadenaConexionPostgreSql(cadenaConexion, esquema);
        ReemplazarSingleton(new ConfiguracionMensajeriaContextoDB { Esquema = esquema });
        ReemplazarSingleton(new ConfiguracionInicializacionEsquemaMensajeria
        {
            Proveedor = ProveedorBaseDatosMensajeria.PostgreSql,
            CadenaConexion = cadenaConexion,
            Esquema = esquema
        });
        servicios.AddDbContext<MensajeriaContextoDB>(opciones => opciones.UseNpgsql(cadenaConexionFinal));
        return this;
    }

    public IMensajeriaBuilder UsarSqlServer(string cadenaConexion)
    {
        return UsarSqlServer(cadenaConexion, null);
    }

    public IMensajeriaBuilder UsarSqlServer(string cadenaConexion, string? esquema)
    {
        ReemplazarSingleton(new ConfiguracionMensajeriaContextoDB { Esquema = esquema });
        ReemplazarSingleton(new ConfiguracionInicializacionEsquemaMensajeria
        {
            Proveedor = ProveedorBaseDatosMensajeria.SqlServer,
            CadenaConexion = cadenaConexion,
            Esquema = esquema
        });
        servicios.AddDbContext<MensajeriaContextoDB>(opciones => opciones.UseSqlServer(cadenaConexion));
        return this;
    }

    public IMensajeriaBuilder ConfigurarLineaConversacion(TimeSpan tiempoMaximoInactividad)
    {
        ReemplazarSingleton(new ConfiguracionLineaConversacion
        {
            TiempoMaximoInactividad = tiempoMaximoInactividad
        });

        return this;
    }

    public IMensajeriaBuilder ConfigurarContexto(Action<IContextoMensajeriaBuilder> configurarContexto)
    {
        ContextoMensajeriaBuilder contextoBuilder = new(servicios);
        configurarContexto(contextoBuilder);
        return this;
    }

    public IMensajeriaBuilder ConfigurarContextoConversacion(ConfiguracionContextoConversacion configuracion)
    {
        ReemplazarSingleton(configuracion);
        return this;
    }

    public IMensajeriaBuilder ConfigurarOrquestadorContexto(ConfiguracionOrquestadorContexto configuracion)
    {
        ReemplazarSingleton(configuracion);
        return this;
    }

    public IMensajeriaBuilder ConfigurarAgrupacionMensajesEntrada(
        ConfiguracionAgrupacionMensajesEntrada configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        configuracion.Validar();
        ReemplazarSingleton(configuracion);
        return this;
    }

    public IMensajeriaBuilder AgregarWorkerOrquestador()
    {
        AgregarHostedServiceSiNoExiste<OrquestadorContextoWorker>();
        return this;
    }

    public IMensajeriaBuilder AgregarWorkerMensajeria<TComunicacion>()
        where TComunicacion : class, IComunicacionMensajeriaAPI
    {
        if (ExisteServicio<IRecepcionMensajeriaAPI>() || ExisteServicio<IEnvioMensajeriaAPI>())
        {
            if (!ExisteServicio<TComunicacion>())
            {
                throw new InvalidOperationException(
                    "Ya existe otra comunicacion de mensajeria registrada.");
            }

            AgregarHostedServiceSiNoExiste<MensajeriaWorker>();
            return this;
        }

        if (!ExisteServicio<TComunicacion>())
        {
            servicios.AddSingleton<TComunicacion>();
        }

        servicios.AddSingleton<IComunicacionMensajeriaAPI>(
            proveedor => proveedor.GetRequiredService<TComunicacion>());
        servicios.AddSingleton<IRecepcionMensajeriaAPI>(
            proveedor => proveedor.GetRequiredService<TComunicacion>());
        servicios.AddSingleton<IEnvioMensajeriaAPI>(
            proveedor => proveedor.GetRequiredService<TComunicacion>());
        AgregarHostedServiceSiNoExiste<MensajeriaWorker>();
        return this;
    }

    public IMensajeriaBuilder AgregarWorkerMensajeriaEntradaInfobip()
    {
        if (ExisteServicio<IRecepcionMensajeriaAPI>()
            && !ExisteServicio<ComunicacionInfobipServicio>())
        {
            throw new InvalidOperationException(
                "Ya existe otra comunicacion de entrada de mensajeria registrada.");
        }

        AgregarSiNoExisteSingleton<IColaRecepcionesInfobipServicio, ColaRecepcionesInfobipServicio>();
        AgregarSiNoExisteScoped<IMapeadorWebhookInfobipServicio, MapeadorWebhookInfobipServicio>();
        AgregarSiNoExisteScoped<IConvertidorMensajeEntranteInfobipServicio, ConvertidorMensajeEntranteInfobipServicio>();
        AgregarSiNoExisteScoped<IRegistrarWebhookInfobipAplicacion, RegistrarWebhookInfobipAplicacion>();
        AgregarSiNoExisteScoped<IObtenerMensajeEntranteInfobipAplicacion, ObtenerMensajeEntranteInfobipAplicacion>();
        AgregarSiNoExisteScoped<IConfirmarMensajeEntranteInfobipAplicacion, ConfirmarMensajeEntranteInfobipAplicacion>();
        AgregarSiNoExisteScoped<ICargarRecepcionesInfobipPendientesAplicacion, CargarRecepcionesInfobipPendientesAplicacion>();
        AgregarSiNoExisteSingleton<ComunicacionInfobipServicio, ComunicacionInfobipServicio>();

        if (!ExisteServicio<IRecepcionMensajeriaAPI>())
        {
            servicios.AddSingleton<IRecepcionMensajeriaAPI>(
                proveedor => proveedor.GetRequiredService<ComunicacionInfobipServicio>());
        }

        if (!ExisteServicio<IConfirmacionMensajeEntranteAPI>())
        {
            servicios.AddSingleton<IConfirmacionMensajeEntranteAPI>(
                proveedor => proveedor.GetRequiredService<ComunicacionInfobipServicio>());
        }

        if (!ExisteServicio<IRecepcionWebhookInfobipAPI>())
        {
            servicios.AddSingleton<IRecepcionWebhookInfobipAPI>(
                proveedor => proveedor.GetRequiredService<ComunicacionInfobipServicio>());
        }

        bool inicializadorRegistrado = servicios.Any(descriptor =>
            descriptor.ServiceType == typeof(IInicializadorModuloEsquemaMensajeria)
            && descriptor.ImplementationType == typeof(InicializadorModuloEsquemaInfobip));
        if (!inicializadorRegistrado)
        {
            servicios.AddSingleton<IInicializadorModuloEsquemaMensajeria,
                InicializadorModuloEsquemaInfobip>();
        }

        AgregarHostedServiceSiNoExiste<MensajeriaWorker>();
        return this;
    }

    public IMensajeriaBuilder AgregarWorkerMensajeriaInfobip(
        Uri servidor,
        string apiKey,
        Action<ConfiguracionClienteInfobip>? configurar = null)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        if (ExisteServicio<IEnvioMensajeriaAPI>()
            && !ExisteServicio<ConfiguracionClienteInfobip>())
        {
            throw new InvalidOperationException(
                "Ya existe otra comunicación de salida de mensajería registrada.");
        }

        ConfiguracionClienteInfobip configuracion = new(servidor, apiKey);
        configurar?.Invoke(configuracion);
        configuracion.Validar();
        AgregarWorkerMensajeriaEntradaInfobip();
        ReemplazarSingleton(configuracion);
        AgregarSiNoExisteSingleton<IAdaptadorMensajeSalidaInfobip,
            AdaptadorMensajeSalidaInfobip>();
        AgregarSiNoExisteScoped<IRegistrarIntentoEnvioInfobipAplicacion,
            RegistrarIntentoEnvioInfobipAplicacion>();

        if (!ExisteServicio<IInfobipWhatsAppCliente>())
        {
            servicios
                .AddHttpClient<IInfobipWhatsAppCliente, InfobipWhatsAppCliente>()
                .RedactLoggedHeaders(["Authorization"]);
        }

        if (!ExisteServicio<IEnvioMensajeriaAPI>())
        {
            servicios.AddSingleton<IEnvioMensajeriaAPI>(
                proveedor => proveedor.GetRequiredService<ComunicacionInfobipServicio>());
        }

        AgregarHostedServiceSiNoExiste<MensajeriaWorker>();
        return this;
    }

    private void RegistrarServiciosBase()
    {
        AgregarSiNoExisteSingleton<IColaEventosMensajeriaEntradaServicio, ColaEventosMensajeriaEntradaServicio>();
        AgregarSiNoExisteSingleton<IColaEventosMensajeriaSalidaServicio, ColaEventosMensajeriaSalidaServicio>();
        AgregarSiNoExisteScoped<IUnitOfWork, UnitOfWork>();
        AgregarSiNoExisteSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
        AgregarSiNoExisteScoped<ICargarEventosMensajeriaPendientesAplicacion, CargarEventosMensajeriaPendientesAplicacion>();
        AgregarSiNoExisteScoped<ICargarEventosMensajeriaSalidaPendientesAplicacion, CargarEventosMensajeriaSalidaPendientesAplicacion>();
        AgregarSiNoExisteScoped<IRegistrarMensajeEntranteAplicacion, RegistrarMensajeEntranteAplicacion>();
        AgregarSiNoExisteScoped<IRegistrarMensajeSalidaAplicacion, RegistrarMensajeSalidaAplicacion>();
        AgregarSiNoExisteScoped<IObtenerMensajeSalidaPendienteAplicacion, ObtenerMensajeSalidaPendienteAplicacion>();
        AgregarSiNoExisteScoped<IRegistrarResultadoEnvioMensajeAplicacion, RegistrarResultadoEnvioMensajeAplicacion>();
        AgregarSiNoExisteScoped<IOrquestarMensajeContextoAplicacion, OrquestarMensajeContextoAplicacion>();
        AgregarSiNoExisteScoped<IRegistrarContextoIAAplicacion, RegistrarContextoIAAplicacion>();
        AgregarSiNoExisteScoped<IConsultaMensajesLineaConversacionAnteriorAplicacion, ConsultaMensajesLineaConversacionAnteriorAplicacion>();
        AgregarSiNoExisteScoped<IEjecucionComandoContextoAplicacion, EjecucionComandoContextoAplicacion>();
        AgregarSiNoExisteScoped<ICompactacionContextoConversacionAplicacion, CompactacionContextoConversacionAplicacion>();
        AgregarSiNoExisteScoped<IRenovarLineaContextoAplicacion, RenovarLineaContextoAplicacion>();
        AgregarSiNoExisteSingleton<IMensajeServicio, MensajeServicio>();
        AgregarSiNoExisteSingleton<IOrquestadorContextoServicio, OrquestadorContextoServicio>();
        AgregarSiNoExisteScoped<IContextoConversacionServicio, ContextoConversacionServicio>();
        AgregarSiNoExisteSingleton<IInicializadorEsquemaMensajeria,
            InicializadorEsquemaMensajeriaCompuesto>();

        AgregarSiNoExisteSingleton(new ConfiguracionMensajeriaContextoDB());
        AgregarSiNoExisteSingleton(new ConfiguracionInicializacionEsquemaMensajeria());

        AgregarSiNoExisteSingleton(new ConfiguracionLineaConversacion
        {
            TiempoMaximoInactividad = TimeSpan.FromHours(24)
        });

        AgregarSiNoExisteSingleton(new ConfiguracionContextoConversacion());
        AgregarSiNoExisteSingleton(new ConfiguracionOrquestadorContexto());
        AgregarSiNoExisteSingleton(new ConfiguracionAgrupacionMensajesEntrada());
    }

    private void AgregarSiNoExisteScoped<TServicio, TImplementacion>()
        where TServicio : class
        where TImplementacion : class, TServicio
    {
        if (!ExisteServicio<TServicio>())
        {
            servicios.AddScoped<TServicio, TImplementacion>();
        }
    }

    private void AgregarSiNoExisteSingleton<TServicio, TImplementacion>()
        where TServicio : class
        where TImplementacion : class, TServicio
    {
        if (!ExisteServicio<TServicio>())
        {
            servicios.AddSingleton<TServicio, TImplementacion>();
        }
    }

    private void AgregarSiNoExisteSingleton<TServicio>(TServicio instancia)
        where TServicio : class
    {
        if (!ExisteServicio<TServicio>())
        {
            servicios.AddSingleton(instancia);
        }
    }

    private void AgregarHostedServiceSiNoExiste<TServicio>()
        where TServicio : class, IHostedService
    {
        Type tipoServicio = typeof(TServicio);
        bool registrado = servicios.Any(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == tipoServicio);

        if (!registrado)
        {
            servicios.AddHostedService<TServicio>();
        }
    }

    private void ReemplazarSingleton<TServicio>(TServicio instancia)
        where TServicio : class
    {
        RemoverServicios<TServicio>();
        servicios.AddSingleton(instancia);
    }

    private bool ExisteServicio<TServicio>()
    {
        Type tipoServicio = typeof(TServicio);
        return servicios.Any(descriptor => descriptor.ServiceType == tipoServicio);
    }

    private void RemoverServicios<TServicio>()
    {
        Type tipoServicio = typeof(TServicio);

        for (int indice = servicios.Count - 1; indice >= 0; indice--)
        {
            if (servicios[indice].ServiceType == tipoServicio)
            {
                servicios.RemoveAt(indice);
            }
        }
    }

    private static string ConstruirCadenaConexionPostgreSql(string cadenaConexion, string? esquema)
    {
        if (string.IsNullOrWhiteSpace(esquema))
        {
            return cadenaConexion;
        }

        NpgsqlConnectionStringBuilder builder = new(cadenaConexion)
        {
            SearchPath = esquema
        };

        return builder.ConnectionString;
    }
}
