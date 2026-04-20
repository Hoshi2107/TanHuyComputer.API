namespace TanHuyComputer.API.Helpers;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public PaginationInfo? Pagination { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Thành công", PaginationInfo? pagination = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Pagination = pagination
        };
    }

    public static ApiResponse<object> ErrorResponse(string message)
    {
        return new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Data = null
        };
    }
}

public class PaginationInfo
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
