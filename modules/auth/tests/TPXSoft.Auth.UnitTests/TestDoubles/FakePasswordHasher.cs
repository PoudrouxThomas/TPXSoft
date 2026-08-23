using TPXSoft.Auth.Domain.Abstractions;

namespace TPXSoft.Auth.UnitTests.TestDoubles;

internal sealed record VerifyCall(string Hash, string Password);

/// <summary>
/// Deterministic, fast stand-in for <see cref="IPasswordHasher"/> -- AuthService's own tests
/// should never pay for real PBKDF2 work. Records every call so tests can assert AuthService's
/// timing-parity dummy-verify path (see AuthService._dummyPasswordHash) actually runs.
/// </summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public List<string> HashedPasswords { get; } = new();

    public List<VerifyCall> VerifyCalls { get; } = new();

    public string Hash(string password)
    {
        HashedPasswords.Add(password);
        return $"fake-hash::{password}";
    }

    public bool Verify(string hash, string password)
    {
        VerifyCalls.Add(new VerifyCall(hash, password));
        return hash == $"fake-hash::{password}";
    }
}
