using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.Aplicacion.Infobip.Cola;
using PER.Mensajeria.Aplicacion.Infobip.Envio;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DTO;
using PER.Mensajeria.Servicio.Infobip;
using ServicioTest.Infraestructura;

namespace ServicioTest;

public class ComunicacionInfobipServicioEnvioTest
{
    [Fact]
    public async Task EnviarMensajeAsync_InfobipAcepta_DebeFinalizarIntentoYRetornarEnviado()
    {
        List<string> pasos = [];
        RegistroIntentosPrueba registroIntentos = new(pasos);
        IAdaptadorMensajeSalidaInfobip adaptador = new AdaptadorPrueba(pasos, exitosa: true);
        IInfobipWhatsAppCliente cliente = new ClientePrueba(
            pasos,
            CrearResultadoAceptado());
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(
            adaptador,
            cliente,
            registroIntentos);
        ComunicacionInfobipServicio servicio = CrearServicio(proveedor, registroLogger);

        DTOResultadoEnvioMensaje resultado = await servicio.EnviarMensajeAsync(
            CrearMensaje(),
            CancellationToken.None);

        Assert.Equal("enviado", resultado.Estado);
        Assert.Equal(71, resultado.IDEnvioMensaje);
        Assert.Equal(["adaptar", "iniciar", "http", "finalizar"], pasos);
        Assert.Equal("aceptado", registroIntentos.EstadoFinal);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EnviarMensajeAsync_AdaptacionFalla_NoDebeLlamarHttp()
    {
        List<string> pasos = [];
        RegistroIntentosPrueba registroIntentos = new(pasos);
        IAdaptadorMensajeSalidaInfobip adaptador = new AdaptadorPrueba(pasos, exitosa: false);
        IInfobipWhatsAppCliente cliente = new ClientePrueba(
            pasos,
            CrearResultadoAceptado());
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(
            adaptador,
            cliente,
            registroIntentos);
        ComunicacionInfobipServicio servicio = CrearServicio(proveedor, registroLogger);

        DTOResultadoEnvioMensaje resultado = await servicio.EnviarMensajeAsync(
            CrearMensaje(),
            CancellationToken.None);

        Assert.Equal("fallido", resultado.Estado);
        Assert.Equal(["adaptar", "fallo_adaptacion"], pasos);
        Assert.DoesNotContain("http", pasos);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EnviarMensajeAsync_RespuestaExitosaIncompleta_DebeMarcarIntentoIncierto()
    {
        List<string> pasos = [];
        RegistroIntentosPrueba registroIntentos = new(pasos);
        IAdaptadorMensajeSalidaInfobip adaptador = new AdaptadorPrueba(pasos, exitosa: true);
        IInfobipWhatsAppCliente cliente = new ClientePrueba(
            pasos,
            new DTOResultadoEnvioInfobipCliente
            {
                EsExitosoHttp = true,
                StatusHttp = 200,
                Respuesta = new DTOInfobipRespuestaEnvio()
            });
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(
            adaptador,
            cliente,
            registroIntentos);
        ComunicacionInfobipServicio servicio = CrearServicio(proveedor, registroLogger);

        DTOResultadoEnvioMensaje resultado = await servicio.EnviarMensajeAsync(
            CrearMensaje(),
            CancellationToken.None);

        Assert.Equal("fallido", resultado.Estado);
        Assert.Equal(["adaptar", "iniciar", "http", "finalizar"], pasos);
        Assert.Equal("incierto", registroIntentos.EstadoFinal);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EnviarMensajeAsync_CancelacionHost_DebeMarcarIntentoInciertoYPropagar()
    {
        List<string> pasos = [];
        RegistroIntentosPrueba registroIntentos = new(pasos);
        IAdaptadorMensajeSalidaInfobip adaptador = new AdaptadorPrueba(pasos, exitosa: true);
        IInfobipWhatsAppCliente cliente = new ClienteCanceladoPrueba(pasos);
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(
            adaptador,
            cliente,
            registroIntentos);
        ComunicacionInfobipServicio servicio = CrearServicio(proveedor, registroLogger);
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            servicio.EnviarMensajeAsync(
                CrearMensaje(),
                cancellationTokenSource.Token));

        Assert.Equal(["adaptar", "iniciar", "http", "incierto"], pasos);
        registroLogger.AssertSinErrores();
    }

    private static ServiceProvider CrearProveedor(
        IAdaptadorMensajeSalidaInfobip adaptador,
        IInfobipWhatsAppCliente cliente,
        IRegistrarIntentoEnvioInfobipAplicacion registroIntentos)
    {
        ServiceCollection servicios = new();
        servicios.AddSingleton(adaptador);
        servicios.AddTransient(_ => cliente);
        servicios.AddSingleton(registroIntentos);
        return servicios.BuildServiceProvider();
    }

    private static ComunicacionInfobipServicio CrearServicio(
        ServiceProvider proveedor,
        RegistroLoggerPrueba registroLogger)
    {
        return new ComunicacionInfobipServicio(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            new ColaRecepcionesInfobipServicio(),
            new LoggerPrueba<ComunicacionInfobipServicio>(registroLogger));
    }

    private static DTOEnvioMensajePendiente CrearMensaje()
    {
        return new DTOEnvioMensajePendiente
        {
            IDEnvioMensaje = 71,
            Canal = "whatsapp",
            Cuenta = "573213155912",
            TipoDestinatario = "telefono",
            IdentificadorDestinatario = "573163432479",
            Mensaje = new DTOMensajeSaliente
            {
                TipoMensaje = "texto",
                Contenido = "Respuesta"
            }
        };
    }

    private static DTOResultadoEnvioInfobipCliente CrearResultadoAceptado()
    {
        return new DTOResultadoEnvioInfobipCliente
        {
            EsExitosoHttp = true,
            StatusHttp = 200,
            Respuesta = new DTOInfobipRespuestaEnvio
            {
                MessageId = "infobip-71",
                Status = new DTOInfobipEstadoEnvio
                {
                    GroupId = 1,
                    GroupName = "PENDING",
                    Id = 7,
                    Name = "PENDING_ENROUTE"
                }
            }
        };
    }

    private sealed class AdaptadorPrueba : IAdaptadorMensajeSalidaInfobip
    {
        private readonly List<string> pasos;
        private readonly bool exitosa;

        public AdaptadorPrueba(List<string> pasos, bool exitosa)
        {
            this.pasos = pasos;
            this.exitosa = exitosa;
        }

        public DTOResultadoAdaptacionEnvioInfobip Adaptar(
            DTOEnvioMensajePendiente mensaje)
        {
            pasos.Add("adaptar");
            return exitosa
                ? new DTOResultadoAdaptacionEnvioInfobip
                {
                    Exitosa = true,
                    Solicitud = new DTOInfobipSolicitudEnvio
                    {
                        Endpoint = "/whatsapp/1/message/text",
                        CuerpoJson = "{}"
                    }
                }
                : new DTOResultadoAdaptacionEnvioInfobip
                {
                    Error = "Mensaje inválido"
                };
        }
    }

    private sealed class ClientePrueba : IInfobipWhatsAppCliente
    {
        private readonly List<string> pasos;
        private readonly DTOResultadoEnvioInfobipCliente resultado;

        public ClientePrueba(
            List<string> pasos,
            DTOResultadoEnvioInfobipCliente resultado)
        {
            this.pasos = pasos;
            this.resultado = resultado;
        }

        public Task<DTOResultadoEnvioInfobipCliente> EnviarAsync(
            DTOInfobipSolicitudEnvio solicitud,
            CancellationToken cancellationToken)
        {
            pasos.Add("http");
            return Task.FromResult(resultado);
        }
    }

    private sealed class ClienteCanceladoPrueba : IInfobipWhatsAppCliente
    {
        private readonly List<string> pasos;

        public ClienteCanceladoPrueba(List<string> pasos)
        {
            this.pasos = pasos;
        }

        public Task<DTOResultadoEnvioInfobipCliente> EnviarAsync(
            DTOInfobipSolicitudEnvio solicitud,
            CancellationToken cancellationToken)
        {
            pasos.Add("http");
            return Task.FromCanceled<DTOResultadoEnvioInfobipCliente>(
                cancellationToken);
        }
    }

    private sealed class RegistroIntentosPrueba :
        IRegistrarIntentoEnvioInfobipAplicacion
    {
        private readonly List<string> pasos;

        public RegistroIntentosPrueba(List<string> pasos)
        {
            this.pasos = pasos;
        }

        public string? EstadoFinal { get; private set; }

        public Task<long> IniciarAsync(
            long idEnvioMensaje,
            DTOInfobipSolicitudEnvio solicitud,
            CancellationToken cancellationToken)
        {
            pasos.Add("iniciar");
            return Task.FromResult(91L);
        }

        public Task RegistrarFalloAdaptacionAsync(
            long idEnvioMensaje,
            string error,
            CancellationToken cancellationToken)
        {
            pasos.Add("fallo_adaptacion");
            return Task.CompletedTask;
        }

        public Task FinalizarAsync(
            long idIntento,
            string estado,
            DTOResultadoEnvioInfobipCliente resultado,
            string? error,
            CancellationToken cancellationToken)
        {
            pasos.Add("finalizar");
            EstadoFinal = estado;
            return Task.CompletedTask;
        }

        public Task MarcarInciertoAsync(
            long idIntento,
            string error,
            CancellationToken cancellationToken)
        {
            pasos.Add("incierto");
            EstadoFinal = "incierto";
            return Task.CompletedTask;
        }
    }
}
