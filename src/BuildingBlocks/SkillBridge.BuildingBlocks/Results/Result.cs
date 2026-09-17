namespace SkillBridge.BuildingBlocks.Results;

public interface IResultResponse<TSelf> where TSelf : IResultResponse<TSelf>
{
    static abstract TSelf FromError(Error error);
}

public class Result : IResultResponse<Result>
{
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Trạng thái Result không hợp lệ.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result FromError(Error error) => Failure(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result, IResultResponse<Result<TValue>>
{
    private readonly TValue? _value;
    public new static Result<TValue> FromError(Error error) => Failure<TValue>(error);

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Không thể truy xuất Value khi Result thất bại.");

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
}
