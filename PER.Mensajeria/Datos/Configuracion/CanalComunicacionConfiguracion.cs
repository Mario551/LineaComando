using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace PER.Mensajeria.Datos.Configuracion;

public class CanalComunicacionConfiguracion : IEntityTypeConfiguration<DAOCanalComunicacion>
{
    public void Configure(EntityTypeBuilder<DAOCanalComunicacion> builder)
    {
        builder.ToTable("per_canales_comunicacion");
        builder.HasKey(canalComunicacion => canalComunicacion.ID);
        builder.Property(canalComunicacion => canalComunicacion.ID).HasColumnName("id");
        builder.Property(canalComunicacion => canalComunicacion.Canal).HasColumnName("canal").HasMaxLength(64);
        builder.Property(canalComunicacion => canalComunicacion.Descripcion).HasColumnName("descripcion");
    }
}
