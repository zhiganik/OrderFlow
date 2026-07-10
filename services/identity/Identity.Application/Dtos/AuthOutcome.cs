namespace Identity.Application.Dtos;

public class AuthOutcome
{
    public bool Succeeded { get; private init; }

    public AuthResult? Result { get; private init; }

    public string? Error { get; private init; }

    public static AuthOutcome Success(AuthResult result) => new() { Succeeded = true, Result = result };

    public static AuthOutcome Failure(string error) => new() { Succeeded = false, Error = error };
}
