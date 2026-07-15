using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace BuilderTest.Infraestructura;

public sealed record EntradaLogPrueba(
    LogLevel Nivel,
    string Categoria,
    string Mensaje,
    Exception? Excepcion);

public sealed class RegistroLoggerPrueba
{
    private readonly ConcurrentQueue<EntradaLogPrueba> entradas = new();
    private readonly ITestOutputHelper? output;

    public RegistroLoggerPrueba(ITestOutputHelper? output = null)
    {
        this.output = output;
    }

    public IReadOnlyList<EntradaLogPrueba> Entradas => entradas.ToList();

    public void Registrar(EntradaLogPrueba entrada)
    {
        entradas.Enqueue(entrada);
        output?.WriteLine($"[{entrada.Nivel}] {entrada.Categoria}: {entrada.Mensaje}{CrearTextoExcepcion(entrada.Excepcion)}");
    }

    public void AssertSinErrores()
    {
        List<EntradaLogPrueba> errores = Entradas
            .Where(entrada => entrada.Nivel >= LogLevel.Error)
            .ToList();

        Assert.True(errores.Count == 0, CrearMensajeErrores(errores));
    }

    private static string CrearTextoExcepcion(Exception? excepcion)
    {
        return excepcion is null ? string.Empty : $" | {excepcion}";
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

public sealed class LoggerProviderPrueba : ILoggerProvider
{
    private readonly RegistroLoggerPrueba registro;

    public LoggerProviderPrueba(RegistroLoggerPrueba registro)
    {
        this.registro = registro;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new LoggerPrueba(registro, categoryName);
    }

    public void Dispose()
    {
    }
}

public sealed class LoggerPrueba : ILogger
{
    private readonly RegistroLoggerPrueba registro;
    private readonly string categoria;

    public LoggerPrueba(RegistroLoggerPrueba registro, string categoria)
    {
        this.registro = registro;
        this.categoria = categoria;
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
