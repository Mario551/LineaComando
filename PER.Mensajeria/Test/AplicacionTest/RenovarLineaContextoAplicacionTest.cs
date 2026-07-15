using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class RenovarLineaContextoAplicacionTest
{
    public static IEnumerable<object[]> Motores => BaseDatosPrueba.Motores;

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task EjecutarAsync_DebeRenovarLineaMoverProcesamientoActualYSerIdempotente(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        DatosRenovacion datos = await PrepararDatosRenovacionAsync(baseDatos, mensaje, procesamiento);
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IRenovarLineaContextoAplicacion aplicacion = new RenovarLineaContextoAplicacion(new UnitOfWork(contexto));
        SolicitudRenovarLineaContexto solicitud = CrearSolicitud(datos, "snapshot v1");

        ResultadoRenovarLineaContexto resultado = await aplicacion.EjecutarAsync(
            solicitud,
            CancellationToken.None);
        ResultadoRenovarLineaContexto reintento = await aplicacion.EjecutarAsync(
            solicitud,
            CancellationToken.None);
        IEstadoContextoConversacionAplicacion estadoAplicacion = new EstadoContextoConversacionAplicacion(
            new UnitOfWork(contexto));
        EstadoContextoConversacion? estadoInicial = await estadoAplicacion.ObtenerInicialAsync(
            resultado.IDLineaConversacion,
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        List<DAOLineaConversacion> lineas = await verificacion.LineasConversacion
            .Where(linea => linea.IDConversacion == datos.IDConversacion)
            .OrderBy(linea => linea.ID)
            .ToListAsync();
        DAOEstadoContextoConversacion estado = await verificacion.EstadosContextoConversacion.SingleAsync();
        DAOMensaje mensajeActual = await verificacion.Mensajes.SingleAsync(
            mensajeActual => mensajeActual.ID == datos.IDMensaje);
        DAOProcesamientoInternoMensaje procesamientoActual = await verificacion.ProcesamientosInternosMensaje.SingleAsync(
            procesamientoActual => procesamientoActual.ID == datos.IDProcesamiento);
        List<DAOEntradaContextoIA> entradasLineaAnterior = await verificacion.EntradasContextoIA
            .Where(entrada => entrada.IDLineaConversacion == datos.IDLineaOrigen)
            .OrderBy(entrada => entrada.Orden)
            .ToListAsync();
        List<DAOEntradaContextoIA> entradasLineaNueva = await verificacion.EntradasContextoIA
            .Where(entrada => entrada.IDLineaConversacion == resultado.IDLineaConversacion)
            .OrderBy(entrada => entrada.Orden)
            .ToListAsync();
        List<DAOMetadataRazonamientoIALineaConversacion> metadataLineaNueva = await verificacion.MetadataRazonamientoIALineaConversacion
            .Where(metadata => metadata.IDLineaConversacion == resultado.IDLineaConversacion)
            .ToListAsync();

        Assert.Equal(2, lineas.Count);
        Assert.False(lineas.Single(linea => linea.ID == datos.IDLineaOrigen).Activa);
        DAOLineaConversacion lineaNueva = lineas.Single(linea => linea.ID == resultado.IDLineaConversacion);
        Assert.True(lineaNueva.Activa);
        Assert.Equal(datos.IDConversacion, lineaNueva.IDConversacion);
        Assert.Equal(estado.ID, lineaNueva.IDEstadoContextoInicial);
        Assert.Equal(datos.IDLineaOrigen, estado.IDLineaConversacionOrigen);
        Assert.Equal(1, estado.Version);
        Assert.Equal("snapshot v1", estado.Contenido);
        Assert.NotNull(estadoInicial);
        Assert.Equal(estado.ID, estadoInicial.ID);
        Assert.Equal("snapshot v1", estadoInicial.Contenido);
        Assert.Equal(lineaNueva.ID, mensajeActual.IDLineaConversacion);
        Assert.Equal("pendiente", procesamientoActual.IDEstadoProcesamientoInternoMensaje);
        Assert.Null(procesamientoActual.Error);
        Assert.Equal(["mensaje_entrada", "limite_ventana"], entradasLineaAnterior.Select(entrada => entrada.IDTipoEntradaContextoIA));
        Assert.Equal(
            ["mensaje_entrada", "decision_comando", "resultado_comando"],
            entradasLineaNueva.Select(entrada => entrada.IDTipoEntradaContextoIA));
        Assert.Equal([1, 2, 3], entradasLineaNueva.Select(entrada => entrada.Orden));
        Assert.Contains(entradasLineaNueva, entrada => entrada.IDTipoEntradaContextoIA == "resultado_comando");
        DAOMetadataRazonamientoIALineaConversacion metadataComando = Assert.Single(metadataLineaNueva);
        Assert.Equal(datos.IDMetadataComando, metadataComando.ID);
        Assert.Equal(resultado.IDEstadoContexto, reintento.IDEstadoContexto);
        Assert.Equal(resultado.IDLineaConversacion, reintento.IDLineaConversacion);
        Assert.Single(await verificacion.EstadosContextoConversacion.ToListAsync());
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task EjecutarAsync_SegundaCompactacion_DebeCrearVersionAcumulativa(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje1, DAOProcesamientoInternoMensaje procesamiento1) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        DatosRenovacion datos1 = await PrepararDatosRenovacionAsync(baseDatos, mensaje1, procesamiento1);
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IRenovarLineaContextoAplicacion aplicacion = new RenovarLineaContextoAplicacion(new UnitOfWork(contexto));
        ResultadoRenovarLineaContexto renovacion1 = await aplicacion.EjecutarAsync(
            CrearSolicitud(datos1, "snapshot v1"),
            CancellationToken.None);
        DatosRenovacion datos2 = await PrepararSegundoProcesamientoAsync(baseDatos, renovacion1);

        ResultadoRenovarLineaContexto renovacion2 = await aplicacion.EjecutarAsync(
            CrearSolicitud(datos2, "snapshot v2"),
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        List<DAOEstadoContextoConversacion> estados = await verificacion.EstadosContextoConversacion
            .OrderBy(estado => estado.Version)
            .ToListAsync();
        DAOEstadoContextoConversacion estado1 = estados[0];
        DAOEstadoContextoConversacion estado2 = estados[1];
        DAOLineaConversacion lineaFinal = await verificacion.LineasConversacion.SingleAsync(
            linea => linea.ID == renovacion2.IDLineaConversacion);
        int entradasPrimerProcesamientoEnLineaIntermedia = await verificacion.EntradasContextoIA.CountAsync(
            entrada => entrada.IDLineaConversacion == renovacion1.IDLineaConversacion
                && entrada.IDProcesamientoInternoMensaje == datos1.IDProcesamiento);

        Assert.Equal(2, estados.Count);
        Assert.Equal(1, estado1.Version);
        Assert.Equal(2, estado2.Version);
        Assert.Equal(estado1.ID, estado2.IDEstadoContextoAnterior);
        Assert.Equal("snapshot v2", estado2.Contenido);
        Assert.Equal(estado2.ID, lineaFinal.IDEstadoContextoInicial);
        Assert.Equal(3, entradasPrimerProcesamientoEnLineaIntermedia);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    private static async Task<DatosRenovacion> PrepararDatosRenovacionAsync(
        BaseDatosPrueba baseDatos,
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOLineaConversacion linea = await contexto.LineasConversacion.SingleAsync(
            lineaActual => lineaActual.ID == mensaje.IDLineaConversacion);
        procesamiento.IDEstadoProcesamientoInternoMensaje = "en_proceso";
        contexto.ProcesamientosInternosMensaje.Update(procesamiento);

        DAOMetadataRazonamientoIALineaConversacion metadataComando = CrearMetadata(
            linea.ID,
            procesamiento.ID,
            mensaje.ID,
            1,
            "Comando");
        DAOMetadataRazonamientoIALineaConversacion metadataLimite = CrearMetadata(
            linea.ID,
            procesamiento.ID,
            mensaje.ID,
            2,
            "LimiteVentanaAlcanzado");
        contexto.MetadataRazonamientoIALineaConversacion.AddRange(metadataComando, metadataLimite);
        await contexto.SaveChangesAsync();

        DateTime fecha = DateTime.Now;
        contexto.EntradasContextoIA.AddRange(
            CrearEntrada(linea.ID, null, null, null, 1, "user", "mensaje_entrada", "historial anterior", fecha.AddMinutes(-5)),
            CrearEntrada(linea.ID, mensaje.ID, procesamiento.ID, null, 2, "user", "mensaje_entrada", mensaje.Contenido, fecha.AddMinutes(-4)),
            CrearEntrada(linea.ID, mensaje.ID, procesamiento.ID, metadataComando.ID, 3, "assistant", "decision_comando", "ejecutar comando", fecha.AddMinutes(-3)),
            CrearEntrada(linea.ID, mensaje.ID, procesamiento.ID, null, 4, "tool", "resultado_comando", "comando completado", fecha.AddMinutes(-2)),
            CrearEntrada(linea.ID, mensaje.ID, procesamiento.ID, metadataLimite.ID, 5, "assistant", "limite_ventana", "limite alcanzado", fecha.AddMinutes(-1)));
        await contexto.SaveChangesAsync();

        return new DatosRenovacion(
            procesamiento.ID,
            mensaje.ID,
            linea.IDConversacion,
            linea.ID,
            metadataComando.ID);
    }

    private static async Task<DatosRenovacion> PrepararSegundoProcesamientoAsync(
        BaseDatosPrueba baseDatos,
        ResultadoRenovarLineaContexto renovacionAnterior)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DateTime fecha = DateTime.Now;
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = renovacionAnterior.IDLineaConversacion,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            Contenido = "segundo mensaje",
            IdentificadorExternoMensaje = $"segundo_{Guid.NewGuid():N}",
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
            IDEstadoProcesamientoInternoMensaje = "en_proceso",
            Intentos = 0,
            FechaCreacion = fecha
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        DAOMetadataRazonamientoIALineaConversacion metadataLimite = CrearMetadata(
            renovacionAnterior.IDLineaConversacion,
            procesamiento.ID,
            mensaje.ID,
            1,
            "LimiteVentanaAlcanzado");
        contexto.MetadataRazonamientoIALineaConversacion.Add(metadataLimite);
        await contexto.SaveChangesAsync();
        contexto.EntradasContextoIA.AddRange(
            CrearEntrada(renovacionAnterior.IDLineaConversacion, mensaje.ID, procesamiento.ID, null, 4, "user", "mensaje_entrada", mensaje.Contenido, fecha),
            CrearEntrada(renovacionAnterior.IDLineaConversacion, mensaje.ID, procesamiento.ID, metadataLimite.ID, 5, "assistant", "limite_ventana", "segundo limite", fecha));
        await contexto.SaveChangesAsync();

        return new DatosRenovacion(
            procesamiento.ID,
            mensaje.ID,
            renovacionAnterior.IDConversacion,
            renovacionAnterior.IDLineaConversacion,
            0);
    }

    private static DAOMetadataRazonamientoIALineaConversacion CrearMetadata(
        long idLinea,
        long idProcesamiento,
        long idMensaje,
        int iteracion,
        string accion)
    {
        return new DAOMetadataRazonamientoIALineaConversacion
        {
            IDLineaConversacion = idLinea,
            IDProcesamientoInternoMensaje = idProcesamiento,
            IDMensaje = idMensaje,
            Proveedor = "fake",
            Modelo = "fake",
            Adaptador = "fake",
            Iteracion = iteracion,
            AccionDecidida = accion,
            FechaCreacion = DateTime.Now
        };
    }

    private static DAOEntradaContextoIA CrearEntrada(
        long idLinea,
        long? idMensaje,
        long? idProcesamiento,
        long? idMetadata,
        int orden,
        string rol,
        string tipo,
        string? contenido,
        DateTime fecha)
    {
        return new DAOEntradaContextoIA
        {
            IDLineaConversacion = idLinea,
            IDMensaje = idMensaje,
            IDProcesamientoInternoMensaje = idProcesamiento,
            IDMetadataRazonamientoIA = idMetadata,
            Orden = orden,
            IDRolContextoIA = rol,
            IDTipoEntradaContextoIA = tipo,
            Contenido = contenido,
            FechaEntrada = fecha,
            FechaCreacion = fecha
        };
    }

    private static SolicitudRenovarLineaContexto CrearSolicitud(
        DatosRenovacion datos,
        string contenido)
    {
        return new SolicitudRenovarLineaContexto
        {
            IDProcesamientoInternoMensaje = datos.IDProcesamiento,
            IDMensaje = datos.IDMensaje,
            IDConversacion = datos.IDConversacion,
            IDLineaConversacionOrigen = datos.IDLineaOrigen,
            Compactacion = ResultadoCompactacionIntencionContexto.Exito(
                contenido,
                new MetadataRazonamientoIAContexto
                {
                    Proveedor = "fake",
                    Modelo = "fake",
                    Adaptador = "fake",
                    Iteracion = 3,
                    AccionDecidida = "Compactar"
                })
        };
    }

    private sealed record DatosRenovacion(
        long IDProcesamiento,
        long IDMensaje,
        long IDConversacion,
        long IDLineaOrigen,
        long IDMetadataComando);
}
