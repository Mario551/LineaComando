using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class TipoEntradaContextoIAConfiguracion : IEntityTypeConfiguration<DAOTipoEntradaContextoIA>
{
    public void Configure(EntityTypeBuilder<DAOTipoEntradaContextoIA> builder)
    {
        builder.ToTable("per_tipos_entrada_contexto_ia");
        builder.HasKey(tipoEntrada => tipoEntrada.ID);
        builder.Property(tipoEntrada => tipoEntrada.ID).HasColumnName("id").HasMaxLength(64);
        builder.Property(tipoEntrada => tipoEntrada.Descripcion).HasColumnName("descripcion");
    }
}
