using AplicacionTest.Infraestructura;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class CargarEventosMensajeriaPendientesAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ProcesamientoPendiente_DebeCrearEventoMensajeria(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await CrearProcesamientoAsync(baseDatos, "pendiente", DateTime.Now);
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<EventoMensajeriaPendiente> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        EventoMensajeriaPendiente evento = Assert.Single(eventos);
        Assert.Equal(mensaje.ID, evento.IDMensaje);
        Assert.Equal(procesamiento.ID, evento.IDProcesamientoInternoMensaje);
        Assert.Equal(mensaje.IDLineaConversacion, evento.IDLineaConversacion);
        Assert.True(evento.IDConversacion > 0);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ProcesamientoEnProceso_DebeCrearEventoMensajeria(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await CrearProcesamientoAsync(baseDatos, "en_proceso", DateTime.Now);
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<EventoMensajeriaPendiente> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        EventoMensajeriaPendiente evento = Assert.Single(eventos);
        Assert.Equal(mensaje.ID, evento.IDMensaje);
        Assert.Equal(procesamiento.ID, evento.IDProcesamientoInternoMensaje);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ProcesadoYError_NoDebeCrearEventosMensajeria(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        await CrearProcesamientoAsync(baseDatos, "procesado", DateTime.Now.AddMinutes(-2));
        await CrearProcesamientoAsync(baseDatos, "error", DateTime.Now.AddMinutes(-1));
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<EventoMensajeriaPendiente> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        Assert.Empty(eventos);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ProcesamientosPendientes_DebeConservarOrdenPorFechaEId(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        DateTime fecha = DateTime.Now.AddMinutes(-10);
        (DAOMensaje mensajePrimero, DAOProcesamientoInternoMensaje procesamientoPrimero) = await CrearProcesamientoAsync(baseDatos, "pendiente", fecha);
        (DAOMensaje mensajeSegundo, DAOProcesamientoInternoMensaje procesamientoSegundo) = await CrearProcesamientoAsync(baseDatos, "pendiente", fecha);
        (DAOMensaje mensajeTercero, DAOProcesamientoInternoMensaje procesamientoTercero) = await CrearProcesamientoAsync(baseDatos, "en_proceso", fecha.AddMinutes(1));
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<EventoMensajeriaPendiente> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        Assert.Collection(
            eventos,
            evento => Assert.Equal(procesamientoPrimero.ID, evento.IDProcesamientoInternoMensaje),
            evento => Assert.Equal(procesamientoSegundo.ID, evento.IDProcesamientoInternoMensaje),
            evento => Assert.Equal(procesamientoTercero.ID, evento.IDProcesamientoInternoMensaje));
    }

    private static ICargarEventosMensajeriaPendientesAplicacion CrearAplicacion(BaseDatosPrueba baseDatos)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IUnitOfWork unitOfWork = new UnitOfWork(contexto);
        return new CargarEventosMensajeriaPendientesAplicacion(unitOfWork);
    }

    private static async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearProcesamientoAsync(
        BaseDatosPrueba baseDatos,
        string estado,
        DateTime fechaCreacion)
    {
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActual = await contexto.ProcesamientosInternosMensaje.FindAsync(procesamiento.ID)
            ?? throw new InvalidOperationException("No se encontro el procesamiento creado para la prueba.");

        procesamientoActual.IDEstadoProcesamientoInternoMensaje = estado;
        procesamientoActual.FechaCreacion = fechaCreacion;
        await contexto.SaveChangesAsync();

        procesamiento.IDEstadoProcesamientoInternoMensaje = estado;
        procesamiento.FechaCreacion = fechaCreacion;
        return (mensaje, procesamiento);
    }
}
