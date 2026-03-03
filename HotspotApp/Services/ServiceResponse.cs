namespace HotspotApp.Services;

public sealed class ServiceResponse<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static ServiceResponse<T> Ok(T data)
    {
        return new ServiceResponse<T> { Success = true, Data = data };
    }

    public static ServiceResponse<T> Fail(string? error)
    {
        return new ServiceResponse<T> { Success = false, Error = error };
    }
}
