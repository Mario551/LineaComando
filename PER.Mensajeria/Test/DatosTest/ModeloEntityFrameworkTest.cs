using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace DatosTest;

public class ModeloEntityFrameworkTest
{
    [Fact]
    public void Modelo_DebeUsarTablasConPrefijoPer()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        Assert.Equal("per_mensajes", ObtenerEntidad(contexto, typeof(DAOMensaje)).GetTableName());
        Assert.Equal("per_lineas_conversacion", ObtenerEntidad(contexto, typeof(DAOLineaConversacion)).GetTableName());
        Assert.Equal("per_procesamientos_internos_mensaje", ObtenerEntidad(contexto, typeof(DAOProcesamientoInternoMensaje)).GetTableName());
        Assert.Equal("per_envios_mensaje", ObtenerEntidad(contexto, typeof(DAOEnvioMensaje)).GetTableName());
    }

    [Fact]
    public void Mensaje_DebeTenerIndiceParcialDeIdempotencia()
    {
        using MensajeriaContextoDB contexto = CrearContexto();
        IEntityType entidadMensaje = ObtenerEntidad(contexto, typeof(DAOMensaje));

        IIndex? indice = entidadMensaje.GetIndexes().SingleOrDefault(indiceActual =>
            indiceActual.Properties.Select(propiedad => propiedad.Name).SequenceEqual(
            [
                nameof(DAOMensaje.IDLineaConversacion),
                nameof(DAOMensaje.IDDireccionMensaje),
                nameof(DAOMensaje.IdentificadorExternoMensaje)
            ]));

        Assert.NotNull(indice);
        Assert.True(indice.IsUnique);
        Assert.Equal("identificador_externo_mensaje IS NOT NULL", indice.GetFilter());
    }

    [Fact]
    public void Fechas_DebenConfigurarseComoTimestampSinZonaHoraria()
    {
        using MensajeriaContextoDB contexto = CrearContexto();
        IEntityType entidadMensaje = ObtenerEntidad(contexto, typeof(DAOMensaje));
        IEntityType entidadEnvio = ObtenerEntidad(contexto, typeof(DAOEnvioMensaje));

        Assert.Equal("timestamp without time zone", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaMensaje))?.GetColumnType());
        Assert.Equal("timestamp without time zone", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaCreacion))?.GetColumnType());
        Assert.Equal("timestamp without time zone", entidadEnvio.FindProperty(nameof(DAOEnvioMensaje.FechaEnviado))?.GetColumnType());
    }

    private static MensajeriaContextoDB CrearContexto()
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseNpgsql("Host=localhost;Database=per_mensajeria_modelo;Username=test;Password=test")
            .Options;

        return new MensajeriaContextoDB(opciones);
    }

    private static IEntityType ObtenerEntidad(MensajeriaContextoDB contexto, Type tipo)
    {
        IEntityType? entidad = contexto.Model.FindEntityType(tipo);
        Assert.NotNull(entidad);
        return entidad;
    }
}
