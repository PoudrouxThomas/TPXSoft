using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Domain.Abstractions;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken refreshToken);
}
