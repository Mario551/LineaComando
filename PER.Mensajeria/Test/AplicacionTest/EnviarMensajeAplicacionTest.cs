using AplicacionTest.Fakes;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.API.Canal;
using PER.Mensajeria.Aplicacion.EnviarMensaje;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class EnviarMensajeAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_EnvioPendiente_DebeLlamarCanalYMarcarEnviado(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion conversacion, DAOLineaConversacion linea, DAOMensaje mensaje, DAOEnvioMensaje envio) = await baseDatos.CrearEnvioPendienteAsync();
        FakeCanalMensajeAPI canalMensajeAPI = new(new DTOResultadoEnvioMensaje
        {
            IDEnvioMensaje = envio.ID,
            Estado = "enviado"
        });

        IEnviarMensajeAplicacion aplicacion = CrearAplicacion(baseDatos, canalMensajeAPI);

        DTOResultadoEnvioMensaje resultado = await aplicacion.EjecutarAsync(envio.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOEnvioMensaje envioActualizado = await contexto.EnviosMensaje.SingleAsync();

        Assert.Equal(envio.ID, resultado.IDEnvioMensaje);
        Assert.Equal("enviado", resultado.Estado);
        Assert.Null(resultado.Error);
        Assert.Equal(1, canalMensajeAPI.CantidadLlamadas);
        Assert.NotNull(canalMensajeAPI.UltimoMensaje);
        Assert.Equal("enviado", envioActualizado.IDEstadoEnvioMensaje);
        Assert.Equal(1, envioActualizado.Intentos);
        Assert.NotNull(envioActualizado.FechaUltimoIntento);
        Assert.NotNull(envioActualizado.FechaEnviado);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_FalloCanal_DebeMarcarFallidoSinFechaEnviado(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion conversacion, DAOLineaConversacion linea, DAOMensaje mensaje, DAOEnvioMensaje envio) = await baseDatos.CrearEnvioPendienteAsync();
        FakeCanalMensajeAPI canalMensajeAPI = new(new DTOResultadoEnvioMensaje
        {
            IDEnvioMensaje = envio.ID,
            Estado = "fallido",
            Error = "fallo canal"
        });

        IEnviarMensajeAplicacion aplicacion = CrearAplicacion(baseDatos, canalMensajeAPI);

        DTOResultadoEnvioMensaje resultado = await aplicacion.EjecutarAsync(envio.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOEnvioMensaje envioActualizado = await contexto.EnviosMensaje.SingleAsync();

        Assert.Equal(envio.ID, resultado.IDEnvioMensaje);
        Assert.Equal("fallido", resultado.Estado);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Error));
        Assert.Equal(1, canalMensajeAPI.CantidadLlamadas);
        Assert.NotNull(canalMensajeAPI.UltimoMensaje);
        Assert.Equal("fallido", envioActualizado.IDEstadoEnvioMensaje);
        Assert.Equal(1, envioActualizado.Intentos);
        Assert.Equal("fallo canal", envioActualizado.Error);
        Assert.NotNull(envioActualizado.FechaUltimoIntento);
        Assert.Null(envioActualizado.FechaEnviado);
    }

    private static IEnviarMensajeAplicacion CrearAplicacion(BaseDatosPrueba baseDatos, ICanalMensajeAPI canalMensajeAPI)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);

        return new EnviarMensajeAplicacion(unitOfWork, canalMensajeAPI);
    }
}
