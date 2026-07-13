namespace Order.Application.Dtos;

public enum CancelOrderOutcome
{
    Canceled,
    NotFound,
    InvalidStatus
}

public class CancelOrderResult
{
    public required CancelOrderOutcome Outcome { get; init; }
    public OrderResponse? Order { get; init; }
}
