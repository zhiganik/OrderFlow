namespace OrderFlow.Shared.Auth;

public interface ICurrentUser
{
    Guid UserId { get; }
    string? Email { get; }
    bool IsAdmin { get; }
}
