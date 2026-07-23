using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace AplicacionTest;

public class CargarEventosMensajeriaSalidaPendientesAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_DebeCargarSoloPendientesEnOrden(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje primero) =
            await baseDatos.CrearEnvioPendienteAsync();
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje segundo) =
            await baseDatos.CrearEnvioPendienteAsync();
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje terminal) =
            await baseDatos.CrearEnvioPendienteAsync();

        await using (MensajeriaContextoDB preparacion = baseDatos.CrearContexto())
        {
            DAOEnvioMensaje envioTerminal = await preparacion.EnviosMensaje
                .SingleAsync(envio => envio.ID == terminal.ID);
            envioTerminal.IDEstadoEnvioMensaje = "enviado";
            envioTerminal.FechaEnviado = DateTime.Now;
            await preparacion.SaveChangesAsync();
        }

        await using MensajeriaContextoDB contextoAplicacion = baseDatos.CrearContexto();
        ICargarEventosMensajeriaSalidaPendientesAplicacion aplicacion =
            new CargarEventosMensajeriaSalidaPendientesAplicacion(
                new UnitOfWork(contextoAplicacion));

        List<EventoMensajeriaSalida> eventos = await aplicacion.EjecutarAsync(
            CancellationToken.None);

        Assert.Collection(
            eventos,
            evento => Assert.Equal(primero.ID, evento.IDEnvioMensaje),
            evento => Assert.Equal(segundo.ID, evento.IDEnvioMensaje));
    }
}
