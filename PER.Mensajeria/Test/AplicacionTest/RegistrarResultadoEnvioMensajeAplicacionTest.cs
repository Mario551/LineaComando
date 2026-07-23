using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class RegistrarResultadoEnvioMensajeAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ResultadoEnviado_DebeCerrarEnvio(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();
        await using MensajeriaContextoDB contextoAplicacion = baseDatos.CrearContexto();
        IRegistrarResultadoEnvioMensajeAplicacion aplicacion =
            new RegistrarResultadoEnvioMensajeAplicacion(new UnitOfWork(contextoAplicacion));

        await aplicacion.EjecutarAsync(
            new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = envio.ID,
                Estado = "enviado"
            },
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        DAOEnvioMensaje envioActual = await verificacion.EnviosMensaje.SingleAsync();
        Assert.Equal("enviado", envioActual.IDEstadoEnvioMensaje);
        Assert.Equal(1, envioActual.Intentos);
        Assert.NotNull(envioActual.FechaUltimoIntento);
        Assert.NotNull(envioActual.FechaEnviado);
        Assert.Null(envioActual.Error);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ResultadoFallido_DebeCerrarEnvioConError(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();
        await using MensajeriaContextoDB contextoAplicacion = baseDatos.CrearContexto();
        IRegistrarResultadoEnvioMensajeAplicacion aplicacion =
            new RegistrarResultadoEnvioMensajeAplicacion(new UnitOfWork(contextoAplicacion));

        await aplicacion.EjecutarAsync(
            new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = envio.ID,
                Estado = "fallido",
                Error = "fallo proveedor"
            },
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        DAOEnvioMensaje envioActual = await verificacion.EnviosMensaje.SingleAsync();
        Assert.Equal("fallido", envioActual.IDEstadoEnvioMensaje);
        Assert.Equal(1, envioActual.Intentos);
        Assert.Equal("fallo proveedor", envioActual.Error);
        Assert.NotNull(envioActual.FechaUltimoIntento);
        Assert.Null(envioActual.FechaEnviado);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_EnvioYaTerminal_DebeSerIdempotente(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();

        await using (MensajeriaContextoDB preparacion = baseDatos.CrearContexto())
        {
            DAOEnvioMensaje envioActual = await preparacion.EnviosMensaje.SingleAsync();
            envioActual.IDEstadoEnvioMensaje = "enviado";
            envioActual.Intentos = 1;
            envioActual.FechaEnviado = DateTime.Now;
            await preparacion.SaveChangesAsync();
        }

        await using MensajeriaContextoDB contextoAplicacion = baseDatos.CrearContexto();
        IRegistrarResultadoEnvioMensajeAplicacion aplicacion =
            new RegistrarResultadoEnvioMensajeAplicacion(new UnitOfWork(contextoAplicacion));
        await aplicacion.EjecutarAsync(
            new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = envio.ID,
                Estado = "fallido",
                Error = "respuesta duplicada"
            },
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        DAOEnvioMensaje envioActualizado = await verificacion.EnviosMensaje.SingleAsync();
        Assert.Equal("enviado", envioActualizado.IDEstadoEnvioMensaje);
        Assert.Equal(1, envioActualizado.Intentos);
        Assert.Null(envioActualizado.Error);
    }
}
