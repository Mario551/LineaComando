using AplicacionTest.Fakes;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class OrquestarMensajeEntradaAplicacionTest
{
    [Fact]
    public async Task EjecutarAsync_EventoEntrada_DebeLlamarContextoRegistrarSalidaYMarcarProcesado()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeRegistrarMensajeSalidaAplicacion registrarSalida = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, registrarSalida);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(registrarSalida.Ejecutado);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.True(await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida") > 0);
        Assert.True(await contexto.EnviosMensaje.CountAsync(envioActual => envioActual.IDEstadoEnvioMensaje == "pendiente") > 0);
    }

    [Fact]
    public async Task EjecutarAsync_ErrorContexto_DebeMarcarProcesamientoErrorEIncrementarIntentos()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, new FakeRegistrarMensajeSalidaErrorAplicacion());

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.Equal("error", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(1, procesamientoActualizado.Intentos);
        Assert.False(string.IsNullOrWhiteSpace(procesamientoActualizado.Error));
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
    }

    private static IOrquestarMensajeEntradaAplicacion CrearAplicacion(
        PostgreSqlPrueba baseDatos,
        IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);

        return new OrquestarMensajeEntradaAplicacion(unitOfWork, registrarMensajeSalidaAplicacion);
    }
}
