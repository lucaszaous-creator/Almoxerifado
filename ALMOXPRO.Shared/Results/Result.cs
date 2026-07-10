namespace ALMOXPRO.Shared.Results;

/// <summary>
/// Resultado padrão de operações da camada de aplicação.
/// Evita o uso de exceções para fluxo de controle.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<string> Errors { get; }
    public string Error => Errors.Count > 0 ? Errors[0] : string.Empty;

    protected Result(bool isSuccess, IEnumerable<string>? errors)
    {
        IsSuccess = isSuccess;
        Errors = (errors ?? []).ToList().AsReadOnly();
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, [error]);
    public static Result Failure(IEnumerable<string> errors) => new(false, errors);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
    public static Result<T> Failure<T>(IEnumerable<string> errors) => Result<T>.Failure(errors);
}

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado com falha.");

    private Result(bool isSuccess, T? value, IEnumerable<string>? errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static new Result<T> Failure(string error) => new(false, default, [error]);
    public static new Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors);
}
