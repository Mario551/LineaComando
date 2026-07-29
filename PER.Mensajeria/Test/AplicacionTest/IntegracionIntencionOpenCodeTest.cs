using System.Net.Http.Headers;
using System.Text;
using AplicacionTest.Infraestructura;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

namespace AplicacionTest;

public class IntegracionIntencionOpenCodeTest
{
    [Fact]
    [Trait("Category", "IntegracionOpenCode")]
    public async Task DecidirAsync_ServidorReal_DebeCompletarSesionEfimera()
    {
        Uri servidor = new(ObtenerVariableObligatoria(
            "OPENCODE_SERVER_LOCAL"));
        string usuario = ObtenerVariableObligatoria(
            "OPENCODE_SERVER_USERNAME");
        string contrasena = ObtenerVariableObligatoria(
            "OPENCODE_SERVER_PASSWORD");
        string agente = ObtenerVariableObligatoria(
            "OPENCODE_SERVER_LOCAL_NOMBRE_AGENTE_TEST");
        ConfiguracionIntencionOpenCode configuracion = new(
            "Para esta prueba responde no_responder porque el mensaje es solo una comprobacion tecnica.",
            agente)
        {
            Servidor = servidor,
            AutenticacionBasica =
                new ConfiguracionAutenticacionBasicaOpenCode(
                    usuario,
                    contrasena),
            Timeout = TimeSpan.FromMinutes(5)
        };
        using HttpClient httpClient = new()
        {
            BaseAddress = NormalizarServidor(servidor),
            Timeout = configuracion.Timeout
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{usuario}:{contrasena}")));
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeCliente cliente = new(
            httpClient,
            new LoggerPrueba<OpenCodeCliente>(registroLogger));
        OpenCodeIntencionContextoServicio servicio = new(
            cliente,
            new OpenCodeAgenteAdaptador(configuracion),
            new LoggerPrueba<OpenCodeIntencionContextoServicio>(
                registroLogger));
        using CancellationTokenSource timeout =
            new(TimeSpan.FromMinutes(6));

        ResultadoIntencionContexto resultado =
            await servicio.DecidirAsync(
                CrearSolicitud(),
                timeout.Token);

        Assert.Equal(
            AccionContextoTipo.NoResponder,
            resultado.TipoAccion);
        Assert.False(
            string.IsNullOrWhiteSpace(
                resultado.InformacionTecnicaLlamadaIA.Proveedor));
        Assert.False(
            string.IsNullOrWhiteSpace(
                resultado.InformacionTecnicaLlamadaIA.Modelo));
        Assert.Contains(
            agente,
            resultado.InformacionTecnicaLlamadaIA.RequestJson);
        Assert.DoesNotContain(
            "\"model\"",
            resultado.InformacionTecnicaLlamadaIA.RequestJson);
        Assert.DoesNotContain(
            "\"directory\"",
            resultado.InformacionTecnicaLlamadaIA.RequestJson);
        Assert.False(
            string.IsNullOrWhiteSpace(
                resultado.InformacionTecnicaLlamadaIA.ResponseJson));
        registroLogger.AssertSinErrores();
    }

    private static SolicitudIntencionContexto CrearSolicitud()
    {
        DateTime fecha = DateTime.UtcNow;
        return new SolicitudIntencionContexto
        {
            Solicitud = new SolicitudContextoConversacion
            {
                IDProcesamientoInternoMensaje = 1,
                IDsProcesamientosInternosMensaje = [1],
                IDMensaje = 2,
                IDConversacion = 3,
                IDLineaConversacion = 4,
                IDCuentaCanal = 5,
                TipoMensaje = "texto",
                Contenido = "Comprobacion tecnica OpenCode",
                FechaMensaje = fecha
            },
            Iteracion = 1,
            MetadataEntradasContextoIA =
            [
                new MetadataEntradaContextoIA
                {
                    ID = 1,
                    IDLineaConversacion = 4,
                    IDMensaje = 2,
                    IDProcesamientoInternoMensaje = 1,
                    Orden = 1,
                    IDRolContextoIA = "user",
                    IDTipoEntradaContextoIA = "mensaje_entrada",
                    Contenido = "Comprobacion tecnica OpenCode",
                    FechaEntrada = fecha,
                    FechaCreacion = fecha
                }
            ]
        };
    }

    private static string ObtenerVariableObligatoria(
        string nombre)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(
                $"La variable de entorno {nombre} es obligatoria para la integracion real con OpenCode.");
        }

        return valor;
    }

    private static Uri NormalizarServidor(Uri servidor)
    {
        string valor = servidor.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? servidor.AbsoluteUri
            : servidor.AbsoluteUri + "/";
        return new Uri(valor);
    }
}
