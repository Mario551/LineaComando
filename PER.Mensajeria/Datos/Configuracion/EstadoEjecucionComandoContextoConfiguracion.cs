using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class EstadoEjecucionComandoContextoConfiguracion : IEntityTypeConfiguration<DAOEstadoEjecucionComandoContexto>
{
    public void Configure(EntityTypeBuilder<DAOEstadoEjecucionComandoContexto> builder)
    {
        builder.ToTable("per_estados_ejecucion_comando_contexto");
        builder.HasKey(estado => estado.ID);
        builder.Property(estado => estado.ID).HasColumnName("id").HasMaxLength(32);
        builder.Property(estado => estado.Descripcion).HasColumnName("descripcion");
    }
}
