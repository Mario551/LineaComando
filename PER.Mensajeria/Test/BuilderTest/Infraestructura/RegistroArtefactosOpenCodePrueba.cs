using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

namespace BuilderTest.Infraestructura;

public sealed class RegistroArtefactosOpenCodePrueba
{
    private static readonly JsonSerializerOptions OpcionesJson =
        new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true
        };

    private readonly SemaphoreSlim sincronizacion = new(1, 1);
    private readonly List<LlamadaOpenCodePrueba> llamadas = [];
    private int secuencia;

    public RegistroArtefactosOpenCodePrueba()
    {
        string fecha = DateTime.Now.ToString(
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture);
        Directorio = Path.Combine(
            Path.GetTempPath(),
            $"per_mensajeria_opencode_{fecha}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Directorio);
    }

    public string Directorio { get; }

    public IReadOnlyList<LlamadaOpenCodePrueba> Llamadas
    {
        get
        {
            lock (llamadas)
            {
                return llamadas.ToList();
            }
        }
    }

    public Task GuardarEjecucionAsync(
        object ejecucion,
        CancellationToken cancellationToken = default)
    {
        return GuardarJsonAsync(
            "ejecucion.json",
            ejecucion,
            cancellationToken);
    }

    public async Task GuardarPreflightExitosoAsync(
        HttpStatusCode codigoEstado,
        string respuesta,
        CancellationToken cancellationToken = default)
    {
        await GuardarContenidoAsync(
            "preflight_health_response.json",
            FormatearJson(respuesta),
            cancellationToken);
        await GuardarJsonAsync(
            "preflight_health_metadata.json",
            new
            {
                exitoso = true,
                codigoEstado = (int)codigoEstado
            },
            cancellationToken);
    }

    public async Task GuardarPreflightFallidoAsync(
        string error,
        HttpStatusCode? codigoEstado = null,
        string? respuesta = null,
        CancellationToken cancellationToken = default)
    {
        await GuardarContenidoAsync(
            "preflight_health_error.txt",
            error,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(respuesta))
        {
            await GuardarContenidoAsync(
                "preflight_health_response.json",
                FormatearJson(respuesta),
                cancellationToken);
        }

        await GuardarJsonAsync(
            "preflight_health_metadata.json",
            new
            {
                exitoso = false,
                codigoEstado = codigoEstado is null
                    ? (int?)null
                    : (int)codigoEstado.Value,
                error
            },
            cancellationToken);
    }

    public async Task RegistrarLlamadaAsync<TRespuesta>(
        string operacion,
        string proposito,
        int iteracion,
        string? idSesion,
        DateTime fechaInicio,
        DateTime fechaFin,
        ResultadoOpenCodeCliente<TRespuesta> resultado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operacion);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposito);
        ArgumentNullException.ThrowIfNull(resultado);

        await sincronizacion.WaitAsync();
        try
        {
            int numero = ++secuencia;
            LlamadaOpenCodePrueba llamada = new(
                numero,
                operacion,
                proposito,
                iteracion,
                idSesion,
                resultado.Exitoso,
                resultado.CodigoEstado is null
                    ? null
                    : (int)resultado.CodigoEstado.Value,
                resultado.TipoError,
                resultado.Error,
                fechaInicio,
                fechaFin);

            lock (llamadas)
            {
                llamadas.Add(llamada);
            }

            string prefijo = $"llamada_{numero:000}";
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, $"{prefijo}_request.json"),
                FormatearJson(resultado.SolicitudJson),
                Encoding.UTF8);
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, $"{prefijo}_response.json"),
                FormatearJson(resultado.RespuestaJson),
                Encoding.UTF8);
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, $"{prefijo}_metadata.json"),
                JsonSerializer.Serialize(llamada, OpcionesJson),
                Encoding.UTF8);
        }
        finally
        {
            sincronizacion.Release();
        }
    }

    public async Task RegistrarExcepcionAsync(
        string operacion,
        string proposito,
        int iteracion,
        string? idSesion,
        DateTime fechaInicio,
        Exception excepcion)
    {
        ArgumentNullException.ThrowIfNull(excepcion);

        await sincronizacion.WaitAsync();
        try
        {
            int numero = ++secuencia;
            LlamadaOpenCodePrueba llamada = new(
                numero,
                operacion,
                proposito,
                iteracion,
                idSesion,
                false,
                null,
                excepcion.GetType().Name,
                excepcion.Message,
                fechaInicio,
                DateTime.UtcNow);

            lock (llamadas)
            {
                llamadas.Add(llamada);
            }

            string prefijo = $"llamada_{numero:000}";
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, $"{prefijo}_request.json"),
                string.Empty,
                Encoding.UTF8);
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, $"{prefijo}_response.json"),
                string.Empty,
                Encoding.UTF8);
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, $"{prefijo}_metadata.json"),
                JsonSerializer.Serialize(llamada, OpcionesJson),
                Encoding.UTF8);
        }
        finally
        {
            sincronizacion.Release();
        }
    }

    public Task GuardarManifestAsync(
        CancellationToken cancellationToken = default)
    {
        return GuardarJsonAsync(
            "manifest_llamadas.json",
            Llamadas.OrderBy(llamada => llamada.Secuencia),
            cancellationToken);
    }

    public Task GuardarJsonAsync(
        string nombreArchivo,
        object contenido,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreArchivo);
        ArgumentNullException.ThrowIfNull(contenido);

        return GuardarContenidoAsync(
            nombreArchivo,
            JsonSerializer.Serialize(contenido, OpcionesJson),
            cancellationToken);
    }

    public async Task GuardarErrorAsync(
        Exception excepcion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excepcion);

        await sincronizacion.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                Path.Combine(Directorio, "errores.txt"),
                excepcion + Environment.NewLine,
                Encoding.UTF8,
                cancellationToken);
        }
        finally
        {
            sincronizacion.Release();
        }
    }

    private async Task GuardarContenidoAsync(
        string nombreArchivo,
        string contenido,
        CancellationToken cancellationToken)
    {
        await sincronizacion.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(Directorio, nombreArchivo),
                contenido,
                Encoding.UTF8,
                cancellationToken);
        }
        finally
        {
            sincronizacion.Release();
        }
    }

    private static string FormatearJson(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return contenido ?? string.Empty;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(contenido);
            return JsonSerializer.Serialize(
                documento.RootElement,
                OpcionesJson);
        }
        catch (JsonException)
        {
            return contenido;
        }
    }
}

public sealed record LlamadaOpenCodePrueba(
    int Secuencia,
    string Operacion,
    string Proposito,
    int Iteracion,
    string? IDSesion,
    bool Exitoso,
    int? CodigoEstado,
    string? TipoError,
    string? Error,
    DateTime FechaInicio,
    DateTime FechaFin);
