using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PER.Mensajeria.Builder.Worker;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Aplicacion.EnviarMensaje;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Contexto;
using PER.Mensajeria.Servicio.Envio;
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

    public IMensajeriaBuilder AgregarWorkerOrquestador()
    {
        servicios.AddHostedService<OrquestadorContextoWorker>();
        return this;
    }

    private void RegistrarServiciosBase()
    {
        AgregarSiNoExisteSingleton<IColaEventosMensajeriaServicio, ColaEventosMensajeriaServicio>();
        AgregarSiNoExisteSingleton<IContextoConversacionActivoServicio, ContextoConversacionActivoServicio>();
        AgregarSiNoExisteScoped<IUnitOfWork, UnitOfWork>();
        AgregarSiNoExisteScoped<ICargarEventosMensajeriaPendientesAplicacion, CargarEventosMensajeriaPendientesAplicacion>();
        AgregarSiNoExisteScoped<IRegistrarMensajeEntranteAplicacion, RegistrarMensajeEntranteAplicacion>();
        AgregarSiNoExisteScoped<IRegistrarMensajeSalidaAplicacion, RegistrarMensajeSalidaAplicacion>();
        AgregarSiNoExisteScoped<IEnviarMensajeAplicacion, EnviarMensajeAplicacion>();
        AgregarSiNoExisteScoped<IOrquestarMensajeEntradaAplicacion, OrquestarMensajeEntradaAplicacion>();
        AgregarSiNoExisteScoped<IMensajeServicio, MensajeServicio>();
        AgregarSiNoExisteScoped<IEnvioMensajeServicio, EnvioMensajeServicio>();
        AgregarSiNoExisteScoped<IOrquestadorContextoServicio, OrquestadorContextoServicio>();
        AgregarSiNoExisteScoped<IContextoConversacionServicio, ContextoConversacionServicio>();

        AgregarSiNoExisteSingleton(new ConfiguracionMensajeriaContextoDB());

        AgregarSiNoExisteSingleton(new ConfiguracionLineaConversacion
        {
            TiempoMaximoInactividad = TimeSpan.FromHours(24)
        });

        AgregarSiNoExisteSingleton(new ConfiguracionContextoConversacion());
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
