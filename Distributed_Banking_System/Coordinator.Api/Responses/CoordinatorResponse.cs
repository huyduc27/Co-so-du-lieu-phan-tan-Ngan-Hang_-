namespace Coordinator.Api.Responses;

public class CoordinatorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}