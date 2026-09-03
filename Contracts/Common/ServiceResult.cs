namespace TrackerMultimedia.Contracts.Common;

/// <summary>
/// Encapsula el resultado de una operación de servicio:
/// éxito con un valor, o error con campo y mensaje para el caller HTTP.
/// </summary>
public sealed class ServiceResult<T>
{
    private ServiceResult() { }

    public T? Value { get; private init; }
    public string? ErrorField { get; private init; }
    public string? ErrorMessage { get; private init; }

    public bool IsSuccess => ErrorMessage is null;

    public static ServiceResult<T> Ok(T value) =>
        new() { Value = value };

    public static ServiceResult<T> Fail(string field, string message) =>
        new() { ErrorField = field, ErrorMessage = message };

    /// <summary>
    /// Reenvía este error como resultado de otro tipo. Los servicios encadenan varias
    /// validaciones que devuelven cosas distintas —un título, un ContentKind, un Guid—
    /// y todas acaban propagándose al mismo resultado de endpoint; sin esto, cada punto
    /// de propagación repetía `Fail(x.ErrorField!, x.ErrorMessage!)` con sus dos `!`.
    /// </summary>
    public ServiceResult<TOther> ToFailure<TOther>()
    {
        if (IsSuccess)
            throw new InvalidOperationException("ToFailure sobre un resultado correcto.");

        return ServiceResult<TOther>.Fail(ErrorField!, ErrorMessage!);
    }
}
