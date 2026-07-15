using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class RegistrarContextoIAAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task ObtenerEntradasAsync_DebeCargarTodaLaLineaConMetadataEnOrdenSinRastreo(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        (DAOMensaje mensajeMismaLinea, DAOProcesamientoInternoMensaje procesamientoMismaLinea) =
            await CrearMensajeAsync(baseDatos, mensaje.IDLineaConversacion, "segundo");
        (DAOMensaje mensajeOtraLinea, DAOProcesamientoInternoMensaje procesamientoOtraLinea) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);
        RegistrarContextoIAAplicacion aplicacion = new(unitOfWork);

        await aplicacion.RegistrarEntradaAsync(
            CrearEntrada(mensaje, procesamiento, "user", "mensaje_entrada", "pregunta"),
            CancellationToken.None);
        EntradaContextoIA decision = await aplicacion.RegistrarDecisionAsync(
            CrearSolicitud(mensaje, procesamiento),
            CrearMetadata(),
            CrearEntrada(mensaje, procesamiento, "assistant", "decision_comando", "decision"),
            CancellationToken.None);
        await aplicacion.RegistrarEntradaAsync(
            CrearEntrada(mensaje, procesamiento, "tool", "resultado_comando", "resultado"),
            CancellationToken.None);
        await aplicacion.RegistrarEntradaAsync(
            CrearEntrada(mensajeMismaLinea, procesamientoMismaLinea, "user", "mensaje_entrada", "segunda pregunta"),
            CancellationToken.None);
        await aplicacion.RegistrarEntradaAsync(
            CrearEntrada(mensajeOtraLinea, procesamientoOtraLinea, "user", "mensaje_entrada", "otra linea"),
            CancellationToken.None);

        IReadOnlyList<EntradaContextoIA> entradas = await aplicacion.ObtenerEntradasAsync(
            mensaje.IDLineaConversacion,
            CancellationToken.None);

        Assert.Equal(4, entradas.Count);
        Assert.Equal([1, 2, 3, 4], entradas.Select(entrada => entrada.Orden));
        Assert.All(entradas, entrada => Assert.Equal(mensaje.IDLineaConversacion, entrada.IDLineaConversacion));
        EntradaContextoIA entradaDecision = Assert.Single(
            entradas,
            entrada => entrada.ID == decision.ID);
        Assert.NotNull(entradaDecision.Metadata);
        Assert.Equal("razonamiento de prueba", entradaDecision.Metadata.Reasoning);
        Assert.Equal("finish", entradaDecision.Metadata.FinishReason);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarDecisionAsync_EntradaInvalida_DebeRevertirMetadataYLiberarRastreo(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);
        RegistrarContextoIAAplicacion aplicacion = new(unitOfWork);
        SolicitudRegistrarEntradaContextoIA entrada = CrearEntrada(
            mensaje,
            procesamiento,
            "rol_inexistente",
            "decision_comando",
            "decision invalida");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => aplicacion.RegistrarDecisionAsync(
                CrearSolicitud(mensaje, procesamiento),
                CrearMetadata(),
                entrada,
                CancellationToken.None));

        Assert.Empty(contexto.ChangeTracker.Entries());
        Assert.Equal(
            0,
            await contexto.MetadataRazonamientoIALineaConversacion.AsNoTracking().CountAsync());
        Assert.Equal(
            0,
            await contexto.EntradasContextoIA.AsNoTracking().CountAsync());
    }

    private static async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearMensajeAsync(
        BaseDatosPrueba baseDatos,
        long idLineaConversacion,
        string sufijo)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DateTime fecha = DateTime.Now;
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = idLineaConversacion,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            Contenido = $"mensaje {sufijo}",
            IdentificadorExternoMensaje = $"contexto_{sufijo}_{Guid.NewGuid():N}",
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
            IDEstadoProcesamientoInternoMensaje = "pendiente",
            FechaCreacion = fecha
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();
        return (mensaje, procesamiento);
    }

    private static SolicitudContextoConversacion CrearSolicitud(
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        return new SolicitudContextoConversacion
        {
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMensaje = mensaje.ID,
            IDLineaConversacion = mensaje.IDLineaConversacion,
            TipoMensaje = mensaje.IDTipoMensaje,
            Contenido = mensaje.Contenido,
            FechaMensaje = mensaje.FechaMensaje
        };
    }

    private static SolicitudRegistrarEntradaContextoIA CrearEntrada(
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento,
        string rol,
        string tipo,
        string contenido)
    {
        return new SolicitudRegistrarEntradaContextoIA
        {
            IDLineaConversacion = mensaje.IDLineaConversacion,
            IDMensaje = mensaje.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDRolContextoIA = rol,
            IDTipoEntradaContextoIA = tipo,
            Contenido = contenido,
            FechaEntrada = mensaje.FechaMensaje
        };
    }

    private static MetadataRazonamientoIAContexto CrearMetadata()
    {
        return new MetadataRazonamientoIAContexto
        {
            Proveedor = "proveedor_prueba",
            Modelo = "modelo_prueba",
            Adaptador = "adaptador_prueba",
            Iteracion = 1,
            AccionDecidida = nameof(AccionContextoTipo.Comando),
            FinishReason = "finish",
            RequestJson = "{}",
            ResponseJson = "{}",
            Content = "decision",
            Reasoning = "razonamiento de prueba"
        };
    }
}
