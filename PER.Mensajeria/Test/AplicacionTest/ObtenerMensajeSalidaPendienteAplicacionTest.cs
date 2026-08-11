using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class ObtenerMensajeSalidaPendienteAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_EnvioPendiente_DebeReconstruirContratoExterno(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion conversacion, DAOLineaConversacion linea, DAOMensaje mensaje, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IUnitOfWork unitOfWork = new UnitOfWork(contexto);
        IObtenerMensajeSalidaPendienteAplicacion aplicacion =
            new ObtenerMensajeSalidaPendienteAplicacion(unitOfWork);

        DTOEnvioMensajePendiente? resultado = await aplicacion.EjecutarAsync(
            envio.ID,
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(envio.ID, resultado.IDEnvioMensaje);
        Assert.Equal("whatsapp", resultado.Canal);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Cuenta));
        Assert.Equal("telefono", resultado.TipoDestinatario);
        Assert.Equal("3001234567", resultado.IdentificadorDestinatario);
        Assert.Equal(conversacion.ID, resultado.Mensaje.IDConversacion);
        Assert.Equal(linea.ID, resultado.Mensaje.IDLineaConversacion);
        Assert.Equal(mensaje.Contenido, resultado.Mensaje.Contenido);
        Assert.Equal(mensaje.FechaMensaje, resultado.Mensaje.FechaMensaje);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_EnvioTerminal_DebeRetornarNull(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();

        await using (MensajeriaContextoDB preparacion = baseDatos.CrearContexto())
        {
            DAOEnvioMensaje envioActual = await preparacion.EnviosMensaje.FindAsync(envio.ID)
                ?? throw new InvalidOperationException("No se encontro el envio.");
            envioActual.IDEstadoEnvioMensaje = "enviado";
            envioActual.FechaEnviado = DateTime.Now;
            await preparacion.SaveChangesAsync();
        }

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IObtenerMensajeSalidaPendienteAplicacion aplicacion =
            new ObtenerMensajeSalidaPendienteAplicacion(new UnitOfWork(contexto));

        DTOEnvioMensajePendiente? resultado = await aplicacion.EjecutarAsync(
            envio.ID,
            CancellationToken.None);

        Assert.Null(resultado);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_SinDestinatarioActivo_DebeFallarAntesDelEnvio(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion conversacion, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();

        await using (MensajeriaContextoDB preparacion = baseDatos.CrearContexto())
        {
            DAOConversacionParticipante relacion = await preparacion
                .ConversacionesParticipantes
                .SingleAsync(actual => actual.IDConversacion == conversacion.ID);
            relacion.Activo = false;
            await preparacion.SaveChangesAsync();
        }

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IObtenerMensajeSalidaPendienteAplicacion aplicacion =
            new ObtenerMensajeSalidaPendienteAplicacion(new UnitOfWork(contexto));

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aplicacion.EjecutarAsync(envio.ID, CancellationToken.None));

        Assert.Contains(
            "exactamente un participante",
            excepcion.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_DosDestinatariosActivos_DebeFallarAntesDelEnvio(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOConversacion conversacion, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();

        await using (MensajeriaContextoDB preparacion = baseDatos.CrearContexto())
        {
            DAOParticipanteConversacion participante = new()
            {
                IDTipoParticipanteConversacion = "telefono",
                IdentificadorParticipante = "3007654321"
            };
            preparacion.ParticipantesConversacion.Add(participante);
            await preparacion.SaveChangesAsync();
            preparacion.ConversacionesParticipantes.Add(new DAOConversacionParticipante
            {
                IDConversacion = conversacion.ID,
                IDParticipanteConversacion = participante.ID,
                FechaUnion = DateTime.Now,
                Activo = true
            });
            await preparacion.SaveChangesAsync();
        }

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        IObtenerMensajeSalidaPendienteAplicacion aplicacion =
            new ObtenerMensajeSalidaPendienteAplicacion(new UnitOfWork(contexto));

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aplicacion.EjecutarAsync(envio.ID, CancellationToken.None));

        Assert.Contains(
            "exactamente un participante",
            excepcion.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
