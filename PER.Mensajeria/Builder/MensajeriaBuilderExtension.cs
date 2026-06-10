using Microsoft.Extensions.DependencyInjection;
using PER.Comandos.LineaComandos.Builder;
using PER.Mensajeria.Datos.Esquema;

namespace PER.Mensajeria.Builder;

public static class MensajeriaBuilderExtension
{
    public static LineaComandoBuilder AgregarMensajeria(
        this LineaComandoBuilder lineaComandoBuilder,
        Action<IMensajeriaBuilder> configurar)
    {
        MensajeriaBuilder builder = new(lineaComandoBuilder.Services);

        if (lineaComandoBuilder.TipoBaseDatos == LineaComandoBuilder.POSTGRESQL)
        {
            builder.UsarPostgreSQL(lineaComandoBuilder.ConnectionString, lineaComandoBuilder.EsquemaBaseDatos);
            lineaComandoBuilder.AgregarInicializadorExterno((_, builderLineaComando, cancellationToken) =>
                new InicializadorEsquemaMensajeriaPostgres(
                    builderLineaComando.ConnectionString,
                    builderLineaComando.EsquemaBaseDatos)
                .InicializarAsync(cancellationToken));
        }
        else if (lineaComandoBuilder.TipoBaseDatos == LineaComandoBuilder.SQLSERVER)
        {
            builder.UsarSqlServer(lineaComandoBuilder.ConnectionString, lineaComandoBuilder.EsquemaBaseDatos);
            lineaComandoBuilder.AgregarInicializadorExterno((_, builderLineaComando, cancellationToken) =>
                new InicializadorEsquemaMensajeriaSqlServer(
                    builderLineaComando.ConnectionString,
                    builderLineaComando.EsquemaBaseDatos)
                .InicializarAsync(cancellationToken));
        }
        else
        {
            throw new NotSupportedException("PER.Mensajeria soporta PostgreSQL y SQL Server en esta etapa.");
        }

        configurar(builder);

        return lineaComandoBuilder;
    }

    public static IServiceCollection AgregarMensajeria(
        this IServiceCollection servicios,
        Action<IMensajeriaBuilder> configurar)
    {
        MensajeriaBuilder builder = new(servicios);
        configurar(builder);
        return servicios;
    }
}
