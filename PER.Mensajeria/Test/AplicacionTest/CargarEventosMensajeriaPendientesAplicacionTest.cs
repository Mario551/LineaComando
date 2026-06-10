using AplicacionTest.Infraestructura;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class CargarEventosMensajeriaPendientesAplicacionTest
{
    [Fact]
    public async Task EjecutarAsync_ProcesamientoPendiente_DebeCrearEventoMensajeria()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await CrearProcesamientoAsync(baseDatos, "pendiente", DateTime.Now);
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<DTOEventoMensajeria> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        DTOEventoMensajeria evento = Assert.Single(eventos);
        Assert.Equal(mensaje.ID, evento.IDMensaje);
        Assert.Equal(procesamiento.ID, evento.IDProcesamientoInternoMensaje);
        Assert.Equal(mensaje.IDLineaConversacion, evento.IDLineaConversacion);
        Assert.True(evento.IDConversacion > 0);
    }

    [Fact]
    public async Task EjecutarAsync_ProcesamientoEnProceso_DebeCrearEventoMensajeria()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await CrearProcesamientoAsync(baseDatos, "en_proceso", DateTime.Now);
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<DTOEventoMensajeria> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        DTOEventoMensajeria evento = Assert.Single(eventos);
        Assert.Equal(mensaje.ID, evento.IDMensaje);
        Assert.Equal(procesamiento.ID, evento.IDProcesamientoInternoMensaje);
    }

    [Fact]
    public async Task EjecutarAsync_ProcesadoYError_NoDebeCrearEventosMensajeria()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        await CrearProcesamientoAsync(baseDatos, "procesado", DateTime.Now.AddMinutes(-2));
        await CrearProcesamientoAsync(baseDatos, "error", DateTime.Now.AddMinutes(-1));
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<DTOEventoMensajeria> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        Assert.Empty(eventos);
    }

    [Fact]
    public async Task EjecutarAsync_ProcesamientosPendientes_DebeConservarOrdenPorFechaEId()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        DateTime fecha = DateTime.Now.AddMinutes(-10);
        (DAOMensaje mensajePrimero, DAOProcesamientoInternoMensaje procesamientoPrimero) = await CrearProcesamientoAsync(baseDatos, "pendiente", fecha);
        (DAOMensaje mensajeSegundo, DAOProcesamientoInternoMensaje procesamientoSegundo) = await CrearProcesamientoAsync(baseDatos, "pendiente", fecha);
        (DAOMensaje mensajeTercero, DAOProcesamientoInternoMensaje procesamientoTercero) = await CrearProcesamientoAsync(baseDatos, "en_proceso", fecha.AddMinutes(1));
        ICargarEventosMensajeriaPendientesAplicacion aplicacion = CrearAplicacion(baseDatos);

        List<DTOEventoMensajeria> eventos = await aplicacion.EjecutarAsync(CancellationToken.None);

        Assert.Collection(
            eventos,
            evento => Assert.Equal(procesamientoPrimero.ID, evento.IDProcesamientoInternoMensaje),
            evento => Assert.Equal(procesamientoSegundo.ID, evento.IDProcesamientoInternoMensaje),
            evento => Assert.Equal(procesamientoTercero.ID, evento.IDProcesamientoInternoMensaje));
    }

    private static ICargarEventosMensajeriaPendientesAplicacion CrearAplicacion(PostgreSqlPrueba baseDatos)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IUnitOfWork unitOfWork = new UnitOfWork(contexto);
        return new CargarEventosMensajeriaPendientesAplicacion(unitOfWork);
    }

    private static async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearProcesamientoAsync(
        PostgreSqlPrueba baseDatos,
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
