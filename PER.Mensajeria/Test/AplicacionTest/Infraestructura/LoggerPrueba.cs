using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AplicacionTest.Infraestructura;

public sealed record EntradaLogPrueba(
    LogLevel Nivel,
    string Categoria,
    string Mensaje,
    Exception? Excepcion);

public sealed class RegistroLoggerPrueba
{
    private readonly ConcurrentQueue<EntradaLogPrueba> entradas = new();

    public IReadOnlyList<EntradaLogPrueba> Entradas => entradas.ToList();

    public void Registrar(EntradaLogPrueba entrada)
    {
        entradas.Enqueue(entrada);
    }

    public void AssertSinErrores()
    {
        List<EntradaLogPrueba> errores = Entradas
            .Where(entrada => entrada.Nivel >= LogLevel.Error)
            .ToList();

        Assert.True(errores.Count == 0, CrearMensajeErrores(errores));
    }

    public void AssertContieneError(string textoEsperado)
    {
        Assert.Contains(
            Entradas,
            entrada => entrada.Nivel >= LogLevel.Error
                && ContieneTexto(entrada, textoEsperado));
    }

    private static bool ContieneTexto(EntradaLogPrueba entrada, string textoEsperado)
    {
        return entrada.Mensaje.Contains(textoEsperado, StringComparison.OrdinalIgnoreCase)
            || (entrada.Excepcion?.ToString().Contains(textoEsperado, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string CrearMensajeErrores(IReadOnlyList<EntradaLogPrueba> errores)
    {
        if (errores.Count == 0)
        {
            return string.Empty;
        }

        IEnumerable<string> mensajes = errores.Select(error => $"[{error.Nivel}] {error.Categoria}: {error.Mensaje} {error.Excepcion}");
        return "No se esperaban registros de error: " + string.Join(Environment.NewLine, mensajes);
    }
}

public sealed class LoggerPrueba<T> : ILogger<T>
{
    private readonly RegistroLoggerPrueba registro;
    private readonly string categoria;

    public LoggerPrueba(RegistroLoggerPrueba registro)
    {
        this.registro = registro;
        categoria = typeof(T).FullName ?? typeof(T).Name;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        registro.Registrar(new EntradaLogPrueba(logLevel, categoria, formatter(state, exception), exception));
    }
}
