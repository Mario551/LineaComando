using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class RolContextoIAConfiguracion : IEntityTypeConfiguration<DAORolContextoIA>
{
    public void Configure(EntityTypeBuilder<DAORolContextoIA> builder)
    {
        builder.ToTable("per_roles_contexto_ia");
        builder.HasKey(rol => rol.ID);
        builder.Property(rol => rol.ID).HasColumnName("id").HasMaxLength(32);
        builder.Property(rol => rol.Descripcion).HasColumnName("descripcion");
    }
}
