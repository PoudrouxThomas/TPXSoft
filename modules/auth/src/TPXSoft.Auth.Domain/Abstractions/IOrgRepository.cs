using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Domain.Abstractions;

public interface IOrgRepository
{
    void Add(Org org);

    Task<Org?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
