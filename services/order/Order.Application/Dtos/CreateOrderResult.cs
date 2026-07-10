namespace Order.Application.Dtos;

public enum CreateOrderOutcome
{
    Created,
    ReplayedFromCache,
    Conflict
}

public class CreateOrderResult
{
    public required CreateOrderOutcome Outcome { get; init; }
    public int StatusCode { get; init; }
    public OrderResponse? Order { get; init; }
}
