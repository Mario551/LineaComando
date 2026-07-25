using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Mensaje;
using ServicioTest.Fakes;

namespace ServicioTest;

public class MensajeServicioTest
{
    [Fact]
    public async Task RecibirAsync_MensajeEntrante_DebeRegistrarYPublicarEvento()
    {
        FakeRegistrarMensajeEntranteAplicacion registrar = new();
        FakeRenovarLineaContextoAplicacion renovar = new();
        ColaEventosMensajeriaEntradaServicio colaEntrada = new();
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        FakeObtenerMensajeSalidaPendienteAplicacion obtenerSalida = new();
        FakeRegistrarResultadoEnvioMensajeAplicacion registrarResultado = new();
        using ServiceProvider proveedor = CrearProveedor(
            registrar,
            renovar,
            obtenerSalida,
            registrarResultado);
        IMensajeServicio servicio = CrearServicio(proveedor, colaEntrada, colaSalida);
        DTORegistrarMensajeEntranteSolicitud solicitud = CrearSolicitudEntrada();

        DTORegistrarMensajeEntranteRespuesta respuesta = await servicio.RecibirAsync(
            solicitud,
            CancellationToken.None);
        EventoMensajeriaEntrada evento = await colaEntrada.ConsumirAsync(CancellationToken.None);

        Assert.True(registrar.Ejecutado);
        Assert.Same(solicitud, registrar.Solicitud);
        Assert.True(respuesta.Registrado);
        Assert.Equal(respuesta.IDMensaje, evento.IDMensaje);
        Assert.Equal(respuesta.IDConversacion, evento.IDConversacion);
        Assert.Equal(respuesta.IDLineaConversacion, evento.IDLineaConversacion);
        Assert.Equal(respuesta.IDProcesamientoInternoMensaje, evento.IDProcesamientoInternoMensaje);
        Assert.Equal("pendiente", evento.IDEstadoProcesamientoInternoMensaje);
    }

    [Fact]
    public async Task RecibirAsync_MensajeDuplicado_NoDebePublicarEvento()
    {
        FakeRegistrarMensajeEntranteAplicacion registrar = new()
        {
            Respuesta = new DTORegistrarMensajeEntranteRespuesta
            {
                IDMensaje = 1,
                IDConversacion = 2,
                IDLineaConversacion = 3,
                IDProcesamientoInternoMensaje = 4,
                Registrado = false
            }
        };
        ColaEventosMensajeriaEntradaServicio colaEntrada = new();
        using ServiceProvider proveedor = CrearProveedor(
            registrar,
            new FakeRenovarLineaContextoAplicacion(),
            new FakeObtenerMensajeSalidaPendienteAplicacion(),
            new FakeRegistrarResultadoEnvioMensajeAplicacion());
        IMensajeServicio servicio = CrearServicio(
            proveedor,
            colaEntrada,
            new ColaEventosMensajeriaSalidaServicio());

        DTORegistrarMensajeEntranteRespuesta respuesta = await servicio.RecibirAsync(
            CrearSolicitudEntrada(),
            CancellationToken.None);

        Assert.False(respuesta.Registrado);
        using CancellationTokenSource cancellationTokenSource =
            new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            colaEntrada.ConsumirAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task RenovarLineaContextoAsync_DebePublicarEventoConLineaNueva()
    {
        FakeRegistrarMensajeEntranteAplicacion registrar = new();
        FakeRenovarLineaContextoAplicacion renovar = new();
        ColaEventosMensajeriaEntradaServicio colaEntrada = new();
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        using ServiceProvider proveedor = CrearProveedor(
            registrar,
            renovar,
            new FakeObtenerMensajeSalidaPendienteAplicacion(),
            new FakeRegistrarResultadoEnvioMensajeAplicacion());
        IMensajeServicio servicio = CrearServicio(proveedor, colaEntrada, colaSalida);
        SolicitudRenovarLineaContexto solicitud = CrearSolicitudRenovacion();

        ResultadoRenovarLineaContexto resultado = await servicio.RenovarLineaContextoAsync(
            solicitud,
            CancellationToken.None);
        EventoMensajeriaEntrada evento = await colaEntrada.ConsumirAsync(CancellationToken.None);

        Assert.Same(solicitud, renovar.Solicitud);
        Assert.Equal(resultado.IDMensaje, evento.IDMensaje);
        Assert.Equal(resultado.IDProcesamientoInternoMensaje, evento.IDProcesamientoInternoMensaje);
        Assert.Equal(resultado.IDConversacion, evento.IDConversacion);
        Assert.Equal(resultado.IDLineaConversacion, evento.IDLineaConversacion);
        Assert.Equal("pendiente", evento.IDEstadoProcesamientoInternoMensaje);
    }

    [Fact]
    public async Task RenovarLineaContextoAsync_Lote_DebePublicarTodosLosEventosConLineaNueva()
    {
        FakeRenovarLineaContextoAplicacion renovar = new();
        ColaEventosMensajeriaEntradaServicio colaEntrada = new();
        using ServiceProvider proveedor = CrearProveedor(
            new FakeRegistrarMensajeEntranteAplicacion(),
            renovar,
            new FakeObtenerMensajeSalidaPendienteAplicacion(),
            new FakeRegistrarResultadoEnvioMensajeAplicacion());
        IMensajeServicio servicio = CrearServicio(
            proveedor,
            colaEntrada,
            new ColaEventosMensajeriaSalidaServicio());
        SolicitudRenovarLineaContexto solicitud = CrearSolicitudRenovacion();
        solicitud.IDsMensajes = [1, 11];
        solicitud.IDsProcesamientosInternosMensaje = [4, 14];

        ResultadoRenovarLineaContexto resultado = await servicio.RenovarLineaContextoAsync(
            solicitud,
            CancellationToken.None);
        EventoMensajeriaEntrada primerEvento = await colaEntrada.ConsumirAsync(
            CancellationToken.None);
        EventoMensajeriaEntrada segundoEvento = await colaEntrada.ConsumirAsync(
            CancellationToken.None);

        Assert.Same(solicitud, renovar.Solicitud);
        Assert.Equal(1, primerEvento.IDMensaje);
        Assert.Equal(4, primerEvento.IDProcesamientoInternoMensaje);
        Assert.Equal(11, segundoEvento.IDMensaje);
        Assert.Equal(14, segundoEvento.IDProcesamientoInternoMensaje);
        Assert.All(
            new[] { primerEvento, segundoEvento },
            evento =>
            {
                Assert.Equal(resultado.IDConversacion, evento.IDConversacion);
                Assert.Equal(resultado.IDLineaConversacion, evento.IDLineaConversacion);
                Assert.Equal("pendiente", evento.IDEstadoProcesamientoInternoMensaje);
            });
    }

    [Fact]
    public async Task EsperarMensajeSalidaAsync_EventoPendiente_DebeRetornarContrato()
    {
        ColaEventosMensajeriaEntradaServicio colaEntrada = new();
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        DTOEnvioMensajePendiente esperado = new()
        {
            IDEnvioMensaje = 25,
            Canal = "whatsapp",
            Cuenta = "cuenta-prueba"
        };
        FakeObtenerMensajeSalidaPendienteAplicacion obtenerSalida = new()
        {
            Resultado = esperado
        };
        using ServiceProvider proveedor = CrearProveedor(
            new FakeRegistrarMensajeEntranteAplicacion(),
            new FakeRenovarLineaContextoAplicacion(),
            obtenerSalida,
            new FakeRegistrarResultadoEnvioMensajeAplicacion());
        IMensajeServicio servicio = CrearServicio(proveedor, colaEntrada, colaSalida);
        colaSalida.Publicar(new EventoMensajeriaSalida
        {
            IDEnvioMensaje = esperado.IDEnvioMensaje,
            FechaCreacion = DateTime.Now
        });

        DTOEnvioMensajePendiente resultado = await servicio.EsperarMensajeSalidaAsync(
            CancellationToken.None);

        Assert.Same(esperado, resultado);
        Assert.Equal(esperado.IDEnvioMensaje, obtenerSalida.IDEnvioMensaje);
    }

    [Fact]
    public async Task RegistrarResultadoEnvioAsync_DebeDelegarEnCasoDeUsoScoped()
    {
        FakeRegistrarResultadoEnvioMensajeAplicacion registrarResultado = new();
        using ServiceProvider proveedor = CrearProveedor(
            new FakeRegistrarMensajeEntranteAplicacion(),
            new FakeRenovarLineaContextoAplicacion(),
            new FakeObtenerMensajeSalidaPendienteAplicacion(),
            registrarResultado);
        IMensajeServicio servicio = CrearServicio(
            proveedor,
            new ColaEventosMensajeriaEntradaServicio(),
            new ColaEventosMensajeriaSalidaServicio());
        DTOResultadoEnvioMensaje resultado = new()
        {
            IDEnvioMensaje = 31,
            Estado = "enviado"
        };

        await servicio.RegistrarResultadoEnvioAsync(resultado, CancellationToken.None);

        Assert.Same(resultado, registrarResultado.Resultado);
    }

    private static IMensajeServicio CrearServicio(
        ServiceProvider proveedor,
        IColaEventosMensajeriaEntradaServicio colaEntrada,
        IColaEventosMensajeriaSalidaServicio colaSalida)
    {
        return new MensajeServicio(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            colaEntrada,
            colaSalida);
    }

    private static ServiceProvider CrearProveedor(
        IRegistrarMensajeEntranteAplicacion registrar,
        IRenovarLineaContextoAplicacion renovar,
        IObtenerMensajeSalidaPendienteAplicacion obtenerSalida,
        IRegistrarResultadoEnvioMensajeAplicacion registrarResultado)
    {
        ServiceCollection servicios = new();
        servicios.AddScoped(_ => registrar);
        servicios.AddScoped(_ => renovar);
        servicios.AddScoped(_ => obtenerSalida);
        servicios.AddScoped(_ => registrarResultado);
        return servicios.BuildServiceProvider();
    }

    private static DTORegistrarMensajeEntranteSolicitud CrearSolicitudEntrada()
    {
        return new DTORegistrarMensajeEntranteSolicitud
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = "whatsapp",
                Cuenta = "cuenta-prueba",
                IdentificadorParticipante = "3001234567",
                TipoParticipante = "telefono",
                TipoMensaje = "texto",
                IdentificadorExternoMensaje = "externo-servicio-1",
                FechaMensaje = DateTime.Now
            }
        };
    }

    private static SolicitudRenovarLineaContexto CrearSolicitudRenovacion()
    {
        return new SolicitudRenovarLineaContexto
        {
            IDProcesamientoInternoMensaje = 4,
            IDMensaje = 1,
            IDConversacion = 2,
            IDLineaConversacionOrigen = 3,
            Compactacion = ResultadoCompactacionIntencionContexto.Exito(
                "snapshot",
                new InformacionTecnicaLlamadaIAContexto
                {
                    Proveedor = "fake",
                    Modelo = "fake",
                    Adaptador = "fake"
                })
        };
    }

    private sealed class FakeObtenerMensajeSalidaPendienteAplicacion
        : IObtenerMensajeSalidaPendienteAplicacion
    {
        public long? IDEnvioMensaje { get; private set; }
        public DTOEnvioMensajePendiente? Resultado { get; set; }

        public Task<DTOEnvioMensajePendiente?> EjecutarAsync(
            long idEnvioMensaje,
            CancellationToken cancellationToken)
        {
            IDEnvioMensaje = idEnvioMensaje;
            return Task.FromResult(Resultado);
        }
    }

    private sealed class FakeRegistrarResultadoEnvioMensajeAplicacion
        : IRegistrarResultadoEnvioMensajeAplicacion
    {
        public DTOResultadoEnvioMensaje? Resultado { get; private set; }

        public Task EjecutarAsync(
            DTOResultadoEnvioMensaje resultado,
            CancellationToken cancellationToken)
        {
            Resultado = resultado;
            return Task.CompletedTask;
        }
    }
}
