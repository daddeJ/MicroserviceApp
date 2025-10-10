namespace Shared.Models;

public class PageApiResponse<T> : ApiResponse<T>
{
    public PaginationMetadata Pagination { get; set; }

    public static PageApiResponse<T> Ok(T data, PaginationMetadata pagination, string? message = null) =>
        new PageApiResponse<T>
        {
            Success = true,
            Message = message ?? "Request successful",
            Data = data,
            Pagination = pagination
        };
}