using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.BuilderInicializador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Registro;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;
using Xunit;

namespace BuilderTest.Infraestructura;

public static class IntegracionCompletaMensajeriaLineaComandoEscenarioPrueba
{
    internal const string CodigoComando = "pedido consultar";
    internal const string Pedido = "54013";
    internal const string EstadoPedido = "despachado";
    internal const string PreferenciaAnterior = "entrega en la tarde";
    internal static async Task RegistrarComandosPruebaAsync(
        IServiceProvider serviceProvider,
        IBuilderInicializador builderInicializador,
        CancellationToken cancellationToken)
    {
        RegistroIntegracionMensajeriaPrueba registro = serviceProvider.GetRequiredService<RegistroIntegracionMensajeriaPrueba>();

        await builderInicializador
            .NewBuilderComando()
            .Argumentos(CodigoComando, "Consulta el estado de un pedido de prueba")
            .Accion(new ConsultarPedidoComando(registro))
            .Resultado(new ProcesadorResultadoPedidoPrueba())
            .RegistrarAsync();
    }

    internal static void ConfigurarBaseDatos(LineaComandoBuilder builder, ConfiguracionBaseDatosPrueba baseDatos)
    {
        if (baseDatos.Motor == MotorIntegracionCompletaPrueba.PostgreSql)
        {
            builder.UsePostgresql(baseDatos.ConnectionStringBase, baseDatos.Esquema);
            return;
        }

        builder.UseSqlServer(baseDatos.ConnectionStringBase, baseDatos.Esquema);
    }

    internal static void ReconfigurarMensajeriaContextoDBParaEsquemaPrueba(IServiceCollection servicios, ConfiguracionBaseDatosPrueba baseDatos)
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
            opciones.ReplaceService<IModelCacheKeyFactory, ModeloCachePorContextoIntegracionPrueba>();

            if (baseDatos.Motor == MotorIntegracionCompletaPrueba.PostgreSql)
            {
                NpgsqlConnectionStringBuilder builderConexion = new(baseDatos.ConnectionStringBase)
                {
                    SearchPath = baseDatos.Esquema
                };
                opciones.UseNpgsql(builderConexion.ConnectionString);
                return;
            }

            opciones.UseSqlServer(baseDatos.ConnectionStringBase);
        });
    }

    internal static async Task CrearCuentaCanalAsync(IServiceProvider serviceProvider, string cuenta)
    {
        using IServiceScope alcance = serviceProvider.CreateScope();
        MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(canalActual => canalActual.Canal == "whatsapp");
        DAOCuentaCanal cuentaCanal = new()
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = $"Cuenta {cuenta}",
            Activa = true
        };

        contexto.CuentasCanal.Add(cuentaCanal);
        await contexto.SaveChangesAsync();
    }

    internal static async Task<CicloAnteriorPrueba> CrearCicloAnteriorAsync(
        IServiceProvider serviceProvider,
        string cuenta)
    {
        using IServiceScope alcance = serviceProvider.CreateScope();
        MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
        DAOCuentaCanal cuentaCanal = await contexto.CuentasCanal.SingleAsync(
            cuentaActual => cuentaActual.Cuenta == cuenta);
        DateTime fecha = DateTime.Now.AddDays(-2);
        DAOParticipanteConversacion participante = new()
        {
            IDTipoParticipanteConversacion = "telefono",
            IdentificadorParticipante = "3001234567"
        };
        contexto.ParticipantesConversacion.Add(participante);
        await contexto.SaveChangesAsync();

        DAOConversacion conversacion = new()
        {
            IDCuentaCanal = cuentaCanal.ID,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };
        contexto.Conversaciones.Add(conversacion);
        await contexto.SaveChangesAsync();
        contexto.ConversacionesParticipantes.Add(new DAOConversacionParticipante
        {
            IDConversacion = conversacion.ID,
            IDParticipanteConversacion = participante.ID,
            FechaUnion = fecha,
            Activo = true
        });

        DAOLineaConversacion linea = new()
        {
            IDConversacion = conversacion.ID,
            FechaInicio = fecha,
            FechaUltimaActividad = fecha.AddMinutes(2),
            Activa = false
        };
        contexto.LineasConversacion.Add(linea);
        await contexto.SaveChangesAsync();

        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = linea.ID,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            TelefonoOrigen = "3001234567",
            TelefonoDestino = "6011234567",
            Contenido = $"Prefiero {PreferenciaAnterior}.",
            IdentificadorExternoMensaje = $"integracion_anterior_{Guid.NewGuid():N}",
            FechaMensaje = fecha,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };
        contexto.Mensajes.Add(mensaje);
        await contexto.SaveChangesAsync();

        DAOProcesamientoInternoMensaje procesamiento = new()
        {
            IDMensaje = mensaje.ID,
            IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
            IDEstadoProcesamientoInternoMensaje = "procesado",
            Intentos = 1,
            FechaCreacion = fecha,
            FechaProcesado = fecha.AddMinutes(2)
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        DAOInformacionTecnicaLlamadaIALineaConversacion informacionTecnica = new()
        {
            IDLineaConversacion = linea.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMensaje = mensaje.ID,
            Proveedor = "prueba_preparacion",
            Modelo = "prueba_preparacion",
            Adaptador = "prueba_preparacion",
            Iteracion = 1,
            AccionDecidida = nameof(AccionContextoTipo.Responder),
            FinishReason = "stop",
            Content = "Preferencia registrada.",
            FechaCreacion = fecha.AddMinutes(1)
        };
        contexto.InformacionTecnicaLlamadasIALineaConversacion.Add(informacionTecnica);
        await contexto.SaveChangesAsync();
        contexto.MetadataEntradasContextoIA.AddRange(
            new DAOMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = mensaje.Contenido,
                FechaEntrada = mensaje.FechaMensaje,
                FechaCreacion = fecha
            },
            new DAOMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDInformacionTecnicaLlamadaIA = informacionTecnica.ID,
                Orden = 2,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "Preferencia registrada.",
                FechaEntrada = fecha.AddMinutes(1),
                FechaCreacion = fecha.AddMinutes(1)
            });
        await contexto.SaveChangesAsync();

        return new CicloAnteriorPrueba(linea.ID, mensaje.ID, procesamiento.ID);
    }

    internal static async Task IniciarHostedServicesAsync(IEnumerable<IHostedService> hostedServices, CancellationToken cancellationToken)
    {
        foreach (IHostedService hostedService in hostedServices)
        {
            await hostedService.StartAsync(cancellationToken);
        }
    }

    internal static async Task DetenerHostedServicesAsync(IReadOnlyList<IHostedService> hostedServices, CancellationToken cancellationToken)
    {
        for (int indice = hostedServices.Count - 1; indice >= 0; indice--)
        {
            try
            {
                await hostedServices[indice].StopAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    internal static DTORegistrarMensajeEntranteSolicitud CrearSolicitudEntrada(string cuenta)
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
                Contenido = $"Consulta el pedido {Pedido}",
                IdentificadorExternoMensaje = $"integracion_{Guid.NewGuid():N}",
                FechaMensaje = DateTime.Now
            }
        };
    }

    internal static async Task<DTORegistrarMensajeEntranteRespuesta> EsperarRegistroEntradaAsync(
        IServiceProvider serviceProvider,
        string identificadorExternoMensaje,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using IServiceScope alcance = serviceProvider.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
            DatosEntradaRegistradaPrueba? datos = await (
                from mensaje in contexto.Mensajes.AsNoTracking()
                join procesamiento in contexto.ProcesamientosInternosMensaje.AsNoTracking()
                    on mensaje.ID equals procesamiento.IDMensaje
                join linea in contexto.LineasConversacion.AsNoTracking()
                    on mensaje.IDLineaConversacion equals linea.ID
                where mensaje.IdentificadorExternoMensaje == identificadorExternoMensaje
                select new DatosEntradaRegistradaPrueba
                {
                    IDMensaje = mensaje.ID,
                    IDProcesamientoInternoMensaje = procesamiento.ID,
                    IDConversacion = linea.IDConversacion,
                    IDLineaConversacion = linea.ID
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (datos is not null)
            {
                return new DTORegistrarMensajeEntranteRespuesta
                {
                    IDMensaje = datos.IDMensaje,
                    IDProcesamientoInternoMensaje = datos.IDProcesamientoInternoMensaje,
                    IDConversacion = datos.IDConversacion,
                    IDLineaConversacion = datos.IDLineaConversacion,
                    Registrado = true
                };
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    internal static async Task<ResultadoFlujoCompletoPrueba> EsperarProcesamientoAsync(
        IServiceProvider serviceProvider,
        long idProcesamientoInternoMensaje,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using IServiceScope alcance = serviceProvider.CreateScope();
                MensajeriaContextoDB contexto =
                    alcance.ServiceProvider.GetRequiredService<
                        MensajeriaContextoDB>();
                DAOProcesamientoInternoMensaje procesamiento =
                    await contexto.ProcesamientosInternosMensaje.SingleAsync(
                        procesamientoActual =>
                            procesamientoActual.ID
                                == idProcesamientoInternoMensaje,
                        cancellationToken);
                List<DAOMensaje> mensajesEntrada =
                    await contexto.Mensajes
                        .Where(mensaje =>
                            mensaje.IDDireccionMensaje == "entrada")
                        .ToListAsync(cancellationToken);
                List<DAOMensaje> mensajesSalida =
                    await contexto.Mensajes
                        .Where(mensaje =>
                            mensaje.IDDireccionMensaje == "salida")
                        .ToListAsync(cancellationToken);
                List<DAOEnvioMensaje> envios =
                    await contexto.EnviosMensaje
                        .ToListAsync(cancellationToken);
                List<DAOInformacionTecnicaLlamadaIALineaConversacion>
                    informacionTecnicaLlamadasIA = await contexto
                        .InformacionTecnicaLlamadasIALineaConversacion
                        .AsNoTracking()
                        .Where(metadata =>
                            metadata.IDProcesamientoInternoMensaje
                                == idProcesamientoInternoMensaje)
                        .OrderBy(metadata => metadata.Iteracion)
                        .ToListAsync(cancellationToken);
                List<DAOMetadataEntradaContextoIA>
                    metadataEntradasContextoIA = await contexto
                        .MetadataEntradasContextoIA
                        .AsNoTracking()
                        .Where(entrada =>
                            entrada.IDProcesamientoInternoMensaje
                                == idProcesamientoInternoMensaje)
                        .OrderBy(entrada => entrada.Orden)
                        .ToListAsync(cancellationToken);
                List<DAOEjecucionComandoContexto>
                    ejecucionesComandoContexto = await contexto
                        .EjecucionesComandoContexto
                        .AsNoTracking()
                        .Where(ejecucion =>
                            ejecucion.IDProcesamientoInternoMensaje
                                == idProcesamientoInternoMensaje)
                        .OrderBy(ejecucion => ejecucion.NumeroIntento)
                        .ToListAsync(cancellationToken);

                if (procesamiento.IDEstadoProcesamientoInternoMensaje
                    == "error")
                {
                    throw new InvalidOperationException(
                        $"El procesamiento quedó en error: {procesamiento.Error}");
                }

                if (procesamiento.IDEstadoProcesamientoInternoMensaje
                        == "procesado"
                    && mensajesSalida.Count > 0
                    && envios.Count > 0
                    && envios.All(envio =>
                        envio.IDEstadoEnvioMensaje == "enviado"))
                {
                    return new ResultadoFlujoCompletoPrueba(
                        procesamiento,
                        mensajesEntrada,
                        mensajesSalida,
                        envios,
                        informacionTecnicaLlamadasIA,
                        metadataEntradasContextoIA,
                        ejecucionesComandoContexto);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarEstadoTimeoutAsync(serviceProvider, idProcesamientoInternoMensaje, logger);
            throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 10 minutos.");
        }

        await RegistrarEstadoTimeoutAsync(serviceProvider, idProcesamientoInternoMensaje, logger);
        throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 10 minutos.");
    }

    internal static async Task RegistrarEstadoTimeoutAsync(
        IServiceProvider serviceProvider,
        long idProcesamientoInternoMensaje,
        ILogger logger)
    {
        try
        {
            using IServiceScope alcance = serviceProvider.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
            DAOProcesamientoInternoMensaje? procesamiento = await contexto.ProcesamientosInternosMensaje
                .AsNoTracking()
                .SingleOrDefaultAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje);
            int mensajesEntrada = await contexto.Mensajes.AsNoTracking().CountAsync(mensaje => mensaje.IDDireccionMensaje == "entrada");
            int mensajesSalida = await contexto.Mensajes.AsNoTracking().CountAsync(mensaje => mensaje.IDDireccionMensaje == "salida");
            int enviosPendientes = await contexto.EnviosMensaje.AsNoTracking().CountAsync(envio => envio.IDEstadoEnvioMensaje == "pendiente");

            logger.LogError(
                "Timeout esperando flujo completo. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, Estado={Estado}, Intentos={Intentos}, Error={Error}, MensajesEntrada={MensajesEntrada}, MensajesSalida={MensajesSalida}, EnviosPendientes={EnviosPendientes}",
                idProcesamientoInternoMensaje,
                procesamiento?.IDEstadoProcesamientoInternoMensaje,
                procesamiento?.Intentos,
                procesamiento?.Error,
                mensajesEntrada,
                mensajesSalida,
                enviosPendientes);
        }
        catch (Exception excepcion)
        {
            logger.LogError(excepcion, "No se pudo registrar el estado final despues del timeout del flujo completo.");
        }
    }

    internal static ConfiguracionBaseDatosPrueba CrearConfiguracionBaseDatos(MotorIntegracionCompletaPrueba motor)
    {
        if (motor == MotorIntegracionCompletaPrueba.PostgreSql)
        {
            return new ConfiguracionBaseDatosPrueba(
                motor,
                LeerVariableObligatoria(
                    "MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL",
                    "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL es obligatoria para el test completo con PostgreSQL."),
                $"test_mensajeria_full_{Guid.NewGuid():N}",
                $"cuenta_full_{Guid.NewGuid():N}");
        }

        return new ConfiguracionBaseDatosPrueba(
            motor,
            LeerVariableObligatoria(
                "MENSAJERIA_COMANDOS_CONEXION_SQLSERVER",
                "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_SQLSERVER es obligatoria para el test completo con SQL Server."),
            $"test_mensajeria_full_sql_{Guid.NewGuid():N}",
            $"cuenta_full_{Guid.NewGuid():N}");
    }

    internal static string LeerVariableObligatoria(string nombre, string mensaje)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        Assert.False(string.IsNullOrWhiteSpace(valor), mensaje);
        return valor!;
    }


    public enum MotorIntegracionCompletaPrueba
    {
        PostgreSql,
        SqlServer
    }

    internal sealed record ConfiguracionBaseDatosPrueba(
        MotorIntegracionCompletaPrueba Motor,
        string ConnectionStringBase,
        string Esquema,
        string CuentaCanal);

    internal sealed record ResultadoFlujoCompletoPrueba(
        DAOProcesamientoInternoMensaje Procesamiento,
        List<DAOMensaje> MensajesEntrada,
        List<DAOMensaje> MensajesSalida,
        List<DAOEnvioMensaje> Envios,
        List<DAOInformacionTecnicaLlamadaIALineaConversacion> InformacionTecnicaLlamadasIA,
        List<DAOMetadataEntradaContextoIA> MetadataEntradasContextoIA,
        List<DAOEjecucionComandoContexto> EjecucionesComandoContexto);

    internal sealed record CicloAnteriorPrueba(
        long IDLineaConversacion,
        long IDMensaje,
        long IDProcesamientoInternoMensaje);

    internal sealed class DatosEntradaRegistradaPrueba
    {
        public long IDMensaje { get; init; }
        public long IDProcesamientoInternoMensaje { get; init; }
        public long IDConversacion { get; init; }
        public long IDLineaConversacion { get; init; }
    }

    internal sealed class ComunicacionMensajeriaIntegracionPrueba
        : IComunicacionMensajeriaAPI
    {
        private readonly Channel<DTORegistrarMensajeEntranteSolicitud> entradas =
            Channel.CreateUnbounded<DTORegistrarMensajeEntranteSolicitud>();
        private readonly object sincronizacion = new();
        private readonly List<DTOEnvioMensajePendiente> mensajesEnviados = [];

        public IReadOnlyList<DTOEnvioMensajePendiente> MensajesEnviados
        {
            get
            {
                lock (sincronizacion)
                {
                    return mensajesEnviados.ToList();
                }
            }
        }

        public ValueTask PublicarEntradaAsync(
            DTORegistrarMensajeEntranteSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            return entradas.Writer.WriteAsync(solicitud, cancellationToken);
        }

        public ValueTask<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
            CancellationToken cancellationToken)
        {
            return entradas.Reader.ReadAsync(cancellationToken);
        }

        async Task<DTORegistrarMensajeEntranteSolicitud> IRecepcionMensajeriaAPI.EsperarMensajeEntranteAsync(
            CancellationToken cancellationToken)
        {
            return await EsperarMensajeEntranteAsync(cancellationToken);
        }

        public Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
            DTOEnvioMensajePendiente mensaje,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (sincronizacion)
            {
                mensajesEnviados.Add(mensaje);
            }

            return Task.FromResult(new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = mensaje.IDEnvioMensaje,
                Estado = "enviado"
            });
        }
    }

    internal sealed class ModeloCachePorContextoIntegracionPrueba : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            return (context.GetType(), context.ContextId.InstanceId, designTime);
        }
    }

    internal sealed class RegistroIntegracionMensajeriaPrueba
    {
        private readonly object sync = new();

        public List<RegistroFiltroPrueba> Filtros { get; } = [];

        public List<RegistroComandoEjecutadoPrueba> ComandosEjecutados { get; } = [];

        public List<string> Operaciones { get; } = [];

        public void RegistrarFiltro(string nombre, int iteracion)
        {
            lock (sync)
            {
                Filtros.Add(new RegistroFiltroPrueba(nombre, iteracion));
            }
        }

        public void RegistrarComandoEjecutado(string pedido, string estado)
        {
            lock (sync)
            {
                ComandosEjecutados.Add(new RegistroComandoEjecutadoPrueba(pedido, estado));
                Operaciones.Add("comando_ejecutado");
            }
        }

    }

    internal sealed record RegistroFiltroPrueba(string Nombre, int Iteracion);

    internal sealed record RegistroComandoEjecutadoPrueba(string Pedido, string Estado);

    internal sealed class PrimerFiltroContextoPrueba : IFiltroContextoConversacion
    {
        private readonly RegistroIntegracionMensajeriaPrueba registro;

        public PrimerFiltroContextoPrueba(RegistroIntegracionMensajeriaPrueba registro)
        {
            this.registro = registro;
        }

        public Task<ResultadoFiltroContexto> EjecutarAsync(EstadoIteracionContextoConversacion estado, CancellationToken cancellationToken)
        {
            registro.RegistrarFiltro("primer_filtro", estado.Iteracion);
            return Task.FromResult(ResultadoFiltroContexto.ContinuarFlujo());
        }
    }

    internal sealed class SegundoFiltroContextoPrueba : IFiltroContextoConversacion
    {
        private readonly RegistroIntegracionMensajeriaPrueba registro;

        public SegundoFiltroContextoPrueba(RegistroIntegracionMensajeriaPrueba registro)
        {
            this.registro = registro;
        }

        public Task<ResultadoFiltroContexto> EjecutarAsync(EstadoIteracionContextoConversacion estado, CancellationToken cancellationToken)
        {
            registro.RegistrarFiltro("segundo_filtro", estado.Iteracion);
            return Task.FromResult(ResultadoFiltroContexto.ContinuarFlujo());
        }
    }

    internal sealed class CatalogoComandosLineaComandoPrueba : IProveedorCatalogoComandoContextoServicio
    {
        private readonly IRegistroComandos<string, ResultadoComando> registroComandos;

        public CatalogoComandosLineaComandoPrueba(IRegistroComandos<string, ResultadoComando> registroComandos)
        {
            this.registroComandos = registroComandos;
        }

        public async Task<IReadOnlyList<ComandoContexto>> ObtenerAsync(
            SolicitudContextoConversacion solicitud,
            CancellationToken cancellationToken)
        {
            IEnumerable<MetadatosComando> comandos = await registroComandos.ObtenerComandosRegistradosAsync(cancellationToken);
            return comandos
                .Where(comando => comando.Activo)
                .Select(comando => new ComandoContexto
                {
                    Codigo = comando.RutaComando,
                    Descripcion = comando.Descripcion ?? string.Empty,
                    Alcance = "Prueba de integracion de pedido",
                    ReglasUso = "Usar solamente si el usuario pide consultar un pedido por numero.",
                    Autorizado = true,
                    Parametros = new Dictionary<string, string>
                    {
                        ["pedido"] = "Numero de pedido a consultar"
                    }
                })
                .ToList();
        }
    }

    internal sealed class ProcesadorResultadoPedidoPrueba : IProcesadorResultadoComando
    {
        public string Tipo => "pedido_prueba";

        public int Version => 1;

        public string Formato => "json";

        public Task<string?> SerializarAsync(object? salida, CancellationToken token = default)
        {
            string? contenido = salida is string salidaTexto
                ? salidaTexto
                : JsonSerializer.Serialize(salida);
            return Task.FromResult<string?>(contenido);
        }

        public Task<object?> DeserializarAsync(string? contenido, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(contenido))
            {
                return Task.FromResult<object?>(null);
            }

            return Task.FromResult<object?>(contenido);
        }
    }

    internal sealed class ConsultarPedidoComando : ComandoBase<string, ResultadoComando>
    {
        private readonly RegistroIntegracionMensajeriaPrueba registro;
        private string pedido = string.Empty;

        public ConsultarPedidoComando(RegistroIntegracionMensajeriaPrueba registro)
        {
            this.registro = registro;
        }

        public override void Preparar(ICollection<Parametro> parametros)
        {
            pedido = parametros
                .Single(parametro => parametro.Nombre == "--pedido")
                .Valor
                ?? string.Empty;
        }

        public override Task<ResultadoComando> EjecutarAsync(string entrada, CancellationToken token = default)
        {
            if (pedido != Pedido)
            {
                return Task.FromResult(ResultadoComando.Fallo($"Pedido de prueba inesperado: {pedido}"));
            }

            registro.RegistrarComandoEjecutado(pedido, EstadoPedido);
            return Task.FromResult(ResultadoComando.Exito($"Pedido {pedido}: {EstadoPedido}"));
        }
    }
}
