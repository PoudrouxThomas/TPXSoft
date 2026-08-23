using Microsoft.EntityFrameworkCore;
using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;
using TPXSoft.Auth.Infrastructure.Persistence;
using TPXSoft.Auth.IntegrationTests.Fixtures;

namespace TPXSoft.Auth.IntegrationTests.Database;

/// <summary>DB-level guarantees -- distinct from AuthService's own duplicate-email check (see
/// TPXSoft.Auth.UnitTests), which only protects the ordinary application code path.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UniqueConstraintTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture;
    private string _connectionString = string.Empty;

    public UniqueConstraintTests(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    public async Task InitializeAsync()
    {
        _connectionString = await _postgresFixture.CreateDatabaseAsync($"tpxsoft_auth_unique_{Guid.NewGuid():N}");
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AuthDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(_connectionString).Options);

    [Fact]
    public async Task Users_InsertingTwoUsersWithTheSameNormalizedEmail_ThrowsDbUpdateException()
    {
        var orgId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            dbContext.Users.Add(User.Create("dup@example.com", "hash-a", orgId, Role.Admin, TimeProvider.System));
            await dbContext.SaveChangesAsync();
        }

        await using var secondDbContext = CreateDbContext();
        secondDbContext.Users.Add(User.Create("dup@example.com", "hash-b", orgId, Role.Member, TimeProvider.System));

        await Assert.ThrowsAsync<DbUpdateException>(() => secondDbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task RefreshTokens_InsertingTwoTokensWithTheSameTokenHash_ThrowsDbUpdateException()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        Guid userId;

        await using (var dbContext = CreateDbContext())
        {
            var user = User.Create("token-owner@example.com", "hash", Guid.NewGuid(), Role.Admin, TimeProvider.System);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            userId = user.Id;

            dbContext.RefreshTokens.Add(RefreshToken.Create(userId, "duplicate-hash", expiresAt));
            await dbContext.SaveChangesAsync();
        }

        await using var secondDbContext = CreateDbContext();
        secondDbContext.RefreshTokens.Add(RefreshToken.Create(userId, "duplicate-hash", expiresAt));

        await Assert.ThrowsAsync<DbUpdateException>(() => secondDbContext.SaveChangesAsync());
    }
}
