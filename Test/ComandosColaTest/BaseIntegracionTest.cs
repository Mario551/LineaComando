using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.Esquema;

namespace ComandosColaTest
{
    /// <summary>
    /// Clase base para pruebas de integración con PostgreSQL.
    /// Inicializa el esquema y proporciona métodos de limpieza.
    /// </summary>
    public abstract class BaseIntegracionTest : IAsyncLifetime
    {
        protected readonly string ConnectionString;
        protected readonly InicializadorEsquema InicializadorEsquema;

        protected BaseIntegracionTest()
        {
            ConnectionString = Environment.GetEnvironmentVariable("LINEA_COMANDOS_CONEXION_POSTGRESQL")
                ?? throw new InvalidOperationException(
                    "La variable de entorno LINEA_COMANDOS_CONEXION_POSTGRESQL no está configurada");

            InicializadorEsquema = new InicializadorEsquema(ConnectionString);
        }

        public virtual async Task InitializeAsync()
        {
            await InicializadorEsquema.InicializarAsync();
            await LimpiarDatosAsync();
        }

        public virtual Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Limpia las tablas de prueba antes de cada test.
        /// </summary>
        protected async Task LimpiarDatosAsync()
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Orden de eliminación respetando foreign keys
            await connection.ExecuteAsync("DELETE FROM cola_comandos;");
            await connection.ExecuteAsync("DELETE FROM comandos_registrados;");
        }

        /// <summary>
        /// Crea una conexión nueva a la base de datos.
        /// </summary>
        protected NpgsqlConnection CrearConexion()
        {
            return new NpgsqlConnection(ConnectionString);
        }
    }
}
