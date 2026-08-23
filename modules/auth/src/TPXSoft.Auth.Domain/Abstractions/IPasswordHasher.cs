using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Domain.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}
