using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using PER.Mensajeria.Datos.Esquema;

namespace PER.Mensajeria.Datos.Infobip.Esquema;

public class InicializadorModuloEsquemaInfobip :
    IInicializadorModuloEsquemaMensajeria
{
    private static readonly string[] TablasEsperadas =
    [
        "per_webhook_receipts_infobip",
        "per_inbound_messages_infobip",
        "per_message_types_infobip",
        "per_message_contexts_infobip",
        "per_message_referrals_infobip",
        "per_text_messages_infobip",
        "per_location_messages_infobip",
        "per_image_messages_infobip",
        "per_document_messages_infobip",
        "per_audio_messages_infobip",
        "per_video_messages_infobip",
        "per_voice_messages_infobip",
        "per_contact_messages_infobip",
        "per_infected_content_messages_infobip",
        "per_button_messages_infobip",
        "per_sticker_messages_infobip",
        "per_interactive_button_reply_messages_infobip",
        "per_interactive_list_reply_messages_infobip",
        "per_flow_reply_messages_infobip",
        "per_payment_confirmation_messages_infobip",
        "per_call_permission_reply_messages_infobip",
        "per_in_thread_authentication_reply_messages_infobip",
        "per_order_messages_infobip",
        "per_reaction_messages_infobip",
        "per_unsupported_messages_infobip",
        "per_shared_contacts_infobip",
        "per_contact_addresses_infobip",
        "per_contact_emails_infobip",
        "per_contact_phones_infobip",
        "per_contact_urls_infobip",
        "per_order_product_items_infobip",
        "per_flow_response_nodes_infobip",
        "per_estados_procesamiento_mensaje_entrante_infobip",
        "per_procesamientos_mensaje_entrante_infobip",
        "per_estados_intento_envio_mensaje_infobip",
        "per_intentos_envio_mensaje_infobip"
    ];

    public async Task InicializarAsync(
        ConfiguracionInicializacionEsquemaMensajeria configuracion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        configuracion.Validar();
        string esquema = configuracion.ObtenerEsquema();
        await using ContextoInfobipInicializacionDB contexto = CrearContexto(
            configuracion,
            esquema);
        await contexto.Database.OpenConnectionAsync(cancellationToken);
        HashSet<string> tablasExistentes = await ObtenerTablasExistentesAsync(
            contexto,
            esquema,
            cancellationToken);

        if (tablasExistentes.Count == TablasEsperadas.Length)
        {
            return;
        }

        if (tablasExistentes.Count > 0)
        {
            string faltantes = string.Join(
                ", ",
                TablasEsperadas.Where(tabla => !tablasExistentes.Contains(tabla)));
            throw new InvalidOperationException(
                $"El modulo Infobip del esquema '{esquema}' esta incompleto. Objetos faltantes: {faltantes}.");
        }

        IModel modelo = contexto.GetService<IDesignTimeModel>().Model;
        IRelationalModel modeloRelacional = modelo.GetRelationalModel();
        IMigrationsModelDiffer diferenciador = contexto.GetService<IMigrationsModelDiffer>();
        IReadOnlyList<MigrationOperation> operaciones = diferenciador
            .GetDifferences(null, modeloRelacional)
            .Where(operacion => operacion is not EnsureSchemaOperation)
            .ToList();
        IMigrationsSqlGenerator generador = contexto.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> comandos = generador.Generate(
            operaciones,
            modelo);

        await using IDbContextTransaction transaccion = await contexto.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (MigrationCommand comando in comandos)
            {
                await EjecutarAsync(
                    contexto.Database.GetDbConnection(),
                    transaccion.GetDbTransaction(),
                    comando.CommandText,
                    cancellationToken);
            }

            await EjecutarAsync(
                contexto.Database.GetDbConnection(),
                transaccion.GetDbTransaction(),
                CrearRelacionesMensajeria(configuracion.Proveedor, esquema),
                cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static ContextoInfobipInicializacionDB CrearContexto(
        ConfiguracionInicializacionEsquemaMensajeria configuracion,
        string esquema)
    {
        DbContextOptionsBuilder<ContextoInfobipInicializacionDB> opciones = new();

        if (configuracion.Proveedor == ProveedorBaseDatosMensajeria.PostgreSql)
        {
            opciones.UseNpgsql(configuracion.CadenaConexion);
        }
        else if (configuracion.Proveedor == ProveedorBaseDatosMensajeria.SqlServer)
        {
            opciones.UseSqlServer(configuracion.CadenaConexion);
        }
        else
        {
            throw new NotSupportedException(
                $"El proveedor '{configuracion.Proveedor}' no esta soportado por Infobip.");
        }

        return new ContextoInfobipInicializacionDB(opciones.Options, esquema);
    }

    private static async Task<HashSet<string>> ObtenerTablasExistentesAsync(
        ContextoInfobipInicializacionDB contexto,
        string esquema,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @esquema
              AND TABLE_TYPE = 'BASE TABLE'
            """;
        DbConnection conexion = contexto.Database.GetDbConnection();
        await using DbCommand comando = conexion.CreateCommand();
        comando.CommandText = sql;
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = "@esquema";
        parametro.Value = esquema;
        comando.Parameters.Add(parametro);
        await using DbDataReader lector = await comando.ExecuteReaderAsync(cancellationToken);
        HashSet<string> tablas = new(StringComparer.OrdinalIgnoreCase);

        while (await lector.ReadAsync(cancellationToken))
        {
            string tabla = lector.GetString(0);
            if (TablasEsperadas.Contains(tabla, StringComparer.OrdinalIgnoreCase))
            {
                tablas.Add(tabla);
            }
        }

        return tablas;
    }

    private static async Task EjecutarAsync(
        DbConnection conexion,
        DbTransaction transaccion,
        string sql,
        CancellationToken cancellationToken)
    {
        await using DbCommand comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CrearRelacionesMensajeria(
        ProveedorBaseDatosMensajeria proveedor,
        string esquema)
    {
        NombresBaseDatosMensajeria nombres = proveedor == ProveedorBaseDatosMensajeria.PostgreSql
            ? NombresBaseDatosMensajeria.Postgres(esquema)
            : NombresBaseDatosMensajeria.SqlServer(esquema);
        string procesamiento = proveedor == ProveedorBaseDatosMensajeria.PostgreSql
            ? $"{nombres.EsquemaSql}.\"per_procesamientos_mensaje_entrante_infobip\""
            : $"{nombres.EsquemaSql}.[per_procesamientos_mensaje_entrante_infobip]";
        string intentoEnvio = proveedor == ProveedorBaseDatosMensajeria.PostgreSql
            ? $"{nombres.EsquemaSql}.\"per_intentos_envio_mensaje_infobip\""
            : $"{nombres.EsquemaSql}.[per_intentos_envio_mensaje_infobip]";

        return $"""
            ALTER TABLE {procesamiento}
            ADD CONSTRAINT per_fk_proc_entrada_infobip_mensaje
            FOREIGN KEY (id_mensaje) REFERENCES {nombres.Mensajes}(id);

            ALTER TABLE {intentoEnvio}
            ADD CONSTRAINT per_fk_intento_envio_infobip_envio
            FOREIGN KEY (id_envio_mensaje) REFERENCES {nombres.EnviosMensaje}(id);
            """;
    }
}
