using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class ConsultaMensajesLineaConversacionAnteriorAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task ObtenerCicloAsync_DebeRecuperarProcesamientosAnterioresCompletosSinCopiarMensajes(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensajeActual, DAOProcesamientoInternoMensaje procesamientoActual) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        (DAOMensaje mensajeOtraConversacion, DAOProcesamientoInternoMensaje procesamientoOtraConversacion) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        DateTime fechaBase = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);
        CicloPersistido cicloAntiguo = await CrearCicloAsync(
            baseDatos,
            mensajeActual.IDLineaConversacion,
            fechaBase,
            "mensaje ciclo antiguo",
            "respuesta ciclo antiguo");
        CicloPersistido cicloReciente = await CrearCicloAsync(
            baseDatos,
            mensajeActual.IDLineaConversacion,
            fechaBase.AddHours(1),
            "mensaje ciclo reciente",
            "respuesta ciclo reciente");
        CicloPersistido cicloPosterior = await CrearCicloAsync(
            baseDatos,
            mensajeActual.IDLineaConversacion,
            new DateTime(2030, 1, 15, 12, 0, 0, DateTimeKind.Unspecified),
            "mensaje ciclo posterior",
            "respuesta ciclo posterior");
        await MarcarProcesamientoTerminalAsync(
            baseDatos,
            procesamientoOtraConversacion.ID,
            "procesado");

        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        IRegistrarContextoIAAplicacion registrar = new RegistrarContextoIAAplicacion(unitOfWorkFactory);
        IConsultaMensajesLineaConversacionAnteriorAplicacion aplicacion =
            new ConsultaMensajesLineaConversacionAnteriorAplicacion(unitOfWorkFactory, registrar);

        long idConversacionActual;
        int mensajesAntes;
        await using (MensajeriaContextoDB contexto = baseDatos.CrearContexto())
        {
            idConversacionActual = await contexto.LineasConversacion
                .Where(linea => linea.ID == mensajeActual.IDLineaConversacion)
                .Select(linea => linea.IDConversacion)
                .SingleAsync();
            mensajesAntes = await contexto.Mensajes.CountAsync();
        }

        IReadOnlyList<MetadataEntradaContextoIA> primerCiclo = await aplicacion.ObtenerCicloAsync(
            idConversacionActual,
            mensajeActual.IDLineaConversacion,
            1,
            CancellationToken.None);
        IReadOnlyList<MetadataEntradaContextoIA> segundoCiclo = await aplicacion.ObtenerCicloAsync(
            idConversacionActual,
            mensajeActual.IDLineaConversacion,
            2,
            CancellationToken.None);
        IReadOnlyList<MetadataEntradaContextoIA> cicloReferenciado = await aplicacion.ObtenerCicloReferenciadoAsync(
            idConversacionActual,
            mensajeActual.IDLineaConversacion,
            cicloReciente.IDLineaConversacion,
            cicloReciente.IDProcesamientoInternoMensaje,
            CancellationToken.None);
        IReadOnlyList<MetadataEntradaContextoIA> sinMasCiclos = await aplicacion.ObtenerCicloAsync(
            idConversacionActual,
            mensajeActual.IDLineaConversacion,
            3,
            CancellationToken.None);

        AssertCiclo(primerCiclo, cicloReciente, "mensaje ciclo reciente", "respuesta ciclo reciente");
        AssertCiclo(segundoCiclo, cicloAntiguo, "mensaje ciclo antiguo", "respuesta ciclo antiguo");
        Assert.Equal(primerCiclo.Select(entrada => entrada.ID), cicloReferenciado.Select(entrada => entrada.ID));
        Assert.Empty(sinMasCiclos);
        Assert.DoesNotContain(primerCiclo, entrada => entrada.IDProcesamientoInternoMensaje == procesamientoActual.ID);
        Assert.DoesNotContain(primerCiclo, entrada => entrada.IDProcesamientoInternoMensaje == procesamientoOtraConversacion.ID);

        await Assert.ThrowsAsync<InvalidOperationException>(() => aplicacion.ObtenerCicloReferenciadoAsync(
            idConversacionActual,
            mensajeActual.IDLineaConversacion,
            mensajeOtraConversacion.IDLineaConversacion,
            procesamientoOtraConversacion.ID,
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => aplicacion.ObtenerCicloReferenciadoAsync(
            idConversacionActual,
            mensajeActual.IDLineaConversacion,
            cicloPosterior.IDLineaConversacion,
            cicloPosterior.IDProcesamientoInternoMensaje,
            CancellationToken.None));

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        Assert.Equal(mensajesAntes, await verificacion.Mensajes.CountAsync());
        Assert.Empty(verificacion.ChangeTracker.Entries());
        Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
        Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
    }

    private static async Task<CicloPersistido> CrearCicloAsync(
        BaseDatosPrueba baseDatos,
        long idLineaActual,
        DateTime fecha,
        string mensajeContenido,
        string respuestaContenido)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        long idConversacion = await contexto.LineasConversacion
            .Where(linea => linea.ID == idLineaActual)
            .Select(linea => linea.IDConversacion)
            .SingleAsync();
        DAOLineaConversacion linea = new()
        {
            IDConversacion = idConversacion,
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
            Contenido = mensajeContenido,
            IdentificadorExternoMensaje = $"consulta_anterior_{Guid.NewGuid():N}",
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
            Proveedor = "proveedor_prueba",
            Modelo = "modelo_prueba",
            Adaptador = "adaptador_prueba",
            Iteracion = 1,
            AccionDecidida = "Responder",
            FinishReason = "stop",
            Reasoning = $"razonamiento {respuestaContenido}",
            FechaCreacion = fecha.AddMinutes(1)
        };
        contexto.InformacionTecnicaLlamadasIALineaConversacion.Add(informacionTecnica);
        await contexto.SaveChangesAsync();

        DateTime fechaCreacionEntrada = fecha.AddSeconds(10);
        contexto.MetadataEntradasContextoIA.AddRange(
            new DAOMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = mensajeContenido,
                FechaEntrada = fecha,
                FechaCreacion = fechaCreacionEntrada
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
                Contenido = respuestaContenido,
                FechaEntrada = fecha.AddMinutes(1),
                FechaCreacion = fechaCreacionEntrada.AddSeconds(1)
            });
        await contexto.SaveChangesAsync();

        return new CicloPersistido(
            linea.ID,
            procesamiento.ID,
            informacionTecnica.ID,
            fecha,
            fechaCreacionEntrada);
    }

    private static async Task MarcarProcesamientoTerminalAsync(
        BaseDatosPrueba baseDatos,
        long idProcesamiento,
        string estado)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamiento = await contexto.ProcesamientosInternosMensaje.SingleAsync(
            procesamientoActual => procesamientoActual.ID == idProcesamiento);
        procesamiento.IDEstadoProcesamientoInternoMensaje = estado;
        procesamiento.FechaProcesado = DateTime.Now;
        await contexto.SaveChangesAsync();
    }

    private static void AssertCiclo(
        IReadOnlyList<MetadataEntradaContextoIA> ciclo,
        CicloPersistido esperado,
        string mensaje,
        string respuesta)
    {
        Assert.Equal(2, ciclo.Count);
        Assert.Equal([1, 2], ciclo.Select(entrada => entrada.Orden));
        Assert.All(ciclo, entrada => Assert.Equal(esperado.IDLineaConversacion, entrada.IDLineaConversacion));
        Assert.All(ciclo, entrada => Assert.Equal(esperado.IDProcesamientoInternoMensaje, entrada.IDProcesamientoInternoMensaje));
        Assert.Equal([mensaje, respuesta], ciclo.Select(entrada => entrada.Contenido));
        Assert.Equal(esperado.FechaMensaje, ciclo[0].FechaEntrada);
        Assert.Equal(esperado.FechaCreacionEntrada, ciclo[0].FechaCreacion);
        Assert.Null(ciclo[0].InformacionTecnicaLlamadaIA);
        Assert.Equal(esperado.IDInformacionTecnicaLlamadaIA, ciclo[1].IDInformacionTecnicaLlamadaIA);
        Assert.Equal($"razonamiento {respuesta}", ciclo[1].InformacionTecnicaLlamadaIA?.Reasoning);
    }

    private sealed record CicloPersistido(
        long IDLineaConversacion,
        long IDProcesamientoInternoMensaje,
        long IDInformacionTecnicaLlamadaIA,
        DateTime FechaMensaje,
        DateTime FechaCreacionEntrada);
}
