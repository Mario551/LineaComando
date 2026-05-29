using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class CuentaCanalConfiguracion : IEntityTypeConfiguration<DAOCuentaCanal>
{
    public void Configure(EntityTypeBuilder<DAOCuentaCanal> builder)
    {
        builder.ToTable("per_cuentas_canal");
        builder.HasKey(cuentaCanal => cuentaCanal.ID);
        builder.Property(cuentaCanal => cuentaCanal.ID).HasColumnName("id");
        builder.Property(cuentaCanal => cuentaCanal.IDCanalComunicacion).HasColumnName("id_canal_comunicacion");
        builder.Property(cuentaCanal => cuentaCanal.Cuenta).HasColumnName("cuenta").HasMaxLength(128);
        builder.Property(cuentaCanal => cuentaCanal.Descripcion).HasColumnName("descripcion");
        builder.Property(cuentaCanal => cuentaCanal.Activa).HasColumnName("activa");

        builder.HasOne<DAOCanalComunicacion>()
            .WithMany()
            .HasForeignKey(cuentaCanal => cuentaCanal.IDCanalComunicacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cuentaCanal => new { cuentaCanal.IDCanalComunicacion, cuentaCanal.Cuenta }).IsUnique();
    }
}
