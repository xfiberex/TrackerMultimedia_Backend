using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TrackerMultimedia.Tests.Helpers;

/// <summary>
/// Un proveedor de log que se queda con lo escrito, para poder afirmar sobre ello.
///
/// Hace falta porque lo que T4-11 arregla **solo se ve en el log**: que un proveedor de
/// búsqueda falle sin tumbar la petición, o que el identificador que recibe el usuario
/// sea el mismo que quedó registrado. Sin capturar la salida, esos dos comportamientos
/// solo pueden comprobarse leyendo la consola a ojo, que es exactamente como estuvieron
/// rotos sin que nadie lo notara.
///
/// Se registra como un <see cref="ILoggerProvider"/> más a través de
/// <c>configureAdditionalTestServices</c>; convive con los del host sin sustituirlos.
///
/// Implementa <see cref="ISupportExternalScope"/>, y no es un detalle: los ámbitos que
/// interesan —<c>TraceId</c>, <c>SpanId</c>, <c>RequestPath</c>— no los abre nadie con
/// <c>BeginScope</c> sobre este logger, sino el propio <c>LoggerFactory</c>, que los
/// reparte a los proveedores por esta interfaz. Sin implementarla, este proveedor veía
/// los mensajes pero **ningún** ámbito, y la prueba de correlación fallaba por un
/// defecto del instrumento y no del código medido.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<RecordedLog> _entries = new();
    private IExternalScopeProvider? _scopeProvider;

    public IReadOnlyCollection<RecordedLog> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    internal string CollectScopes()
    {
        if (_scopeProvider is null)
            return string.Empty;

        var parts = new List<string>();
        _scopeProvider.ForEachScope((scope, state) => state.Add(scope?.ToString() ?? string.Empty), parts);
        return string.Join(" ", parts);
    }

    public void Dispose() { }

    private sealed class RecordingLogger(
        string category,
        ConcurrentQueue<RecordedLog> sink,
        RecordingLoggerProvider owner) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => owner._scopeProvider?.Push(state) ?? NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Enqueue(new RecordedLog(
                category,
                logLevel,
                formatter(state, exception),
                exception,
                owner.CollectScopes()));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }
}

public sealed record RecordedLog(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    string Scopes);
