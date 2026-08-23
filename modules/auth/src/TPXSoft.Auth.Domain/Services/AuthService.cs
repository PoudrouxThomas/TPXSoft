using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Domain.Services;

/// <summary>
/// Application-level orchestration for register/login/refresh/logout/me. Holds the business
/// rules; everything it talks to (repositories, hashing, token issuance, the clock) is a port
/// implemented in Infrastructure/Api.
/// </summary>
public sealed class AuthService
{
    private readonly IOrgRepository _orgRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenFactory _refreshTokenFactory;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly TimeProvider _timeProvider;

    // Lazily computed, valid-format hash used to run a dummy password verification when the
    // looked-up user doesn't exist -- keeps the "unknown email" and "wrong password" login
    // paths taking a comparable amount of time, so a caller can't use response latency as a
    // user-enumeration oracle.
    private string? _dummyPasswordHash;

    public AuthService(
        IOrgRepository orgRepository,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IRefreshTokenFactory refreshTokenFactory,
        IAccessTokenIssuer accessTokenIssuer,
        TimeProvider timeProvider)
    {
        _orgRepository = orgRepository;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _refreshTokenFactory = refreshTokenFactory;
        _accessTokenIssuer = accessTokenIssuer;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AuthTokens>> RegisterAsync(string email, string password, string orgName, CancellationToken cancellationToken)
    {
        var normalizedEmail = Email.Normalize(email);

        if (!Email.IsValid(normalizedEmail) || !PasswordPolicy.IsValid(password) || string.IsNullOrWhiteSpace(orgName))
        {
            return Result<AuthTokens>.Failure(AuthError.ValidationFailed);
        }

        var existing = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return Result<AuthTokens>.Failure(AuthError.EmailAlreadyRegistered);
        }

        var org = Org.Create(orgName, _timeProvider);
        var passwordHash = _passwordHasher.Hash(password);
        var user = User.Create(normalizedEmail, passwordHash, org.Id, Role.Admin, _timeProvider);

        _orgRepository.Add(org);
        _userRepository.Add(user);

        var (refreshToken, plainRefreshToken) = _refreshTokenFactory.Create(user.Id);
        _refreshTokenRepository.Add(refreshToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenIssuer.Issue(user);
        return Result<AuthTokens>.Success(new AuthTokens(accessToken, plainRefreshToken));
    }

    public async Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            _passwordHasher.Verify(EnsureDummyPasswordHash(), password);
            return Result<AuthTokens>.Failure(AuthError.InvalidCredentials);
        }

        if (!_passwordHasher.Verify(user.PasswordHash, password))
        {
            return Result<AuthTokens>.Failure(AuthError.InvalidCredentials);
        }

        // Multiple concurrent sessions per user are allowed -- login never revokes prior tokens.
        var (refreshToken, plainRefreshToken) = _refreshTokenFactory.Create(user.Id);
        _refreshTokenRepository.Add(refreshToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenIssuer.Issue(user);
        return Result<AuthTokens>.Success(new AuthTokens(accessToken, plainRefreshToken));
    }

    public async Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = _refreshTokenFactory.HashToken(refreshToken);
        var existing = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        if (existing is null || !existing.IsActive(now))
        {
            return Result<AuthTokens>.Failure(AuthError.InvalidRefreshToken);
        }

        var user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AuthTokens>.Failure(AuthError.InvalidRefreshToken);
        }

        existing.Revoke(now);
        var (newToken, plainNewToken) = _refreshTokenFactory.Create(user.Id);
        _refreshTokenRepository.Add(newToken);

        // Revoke the old token and persist the new one together, in a single save.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenIssuer.Issue(user);
        return Result<AuthTokens>.Success(new AuthTokens(accessToken, plainNewToken));
    }

    /// <summary>
    /// Idempotent, no-op-safe: an unknown or already-revoked token is not an error, it just
    /// doesn't change anything.
    /// </summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = _refreshTokenFactory.HashToken(refreshToken);
        var existing = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Revoke(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<User>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? Result<User>.Failure(AuthError.InvalidCredentials)
            : Result<User>.Success(user);
    }

    private string EnsureDummyPasswordHash() => _dummyPasswordHash ??= _passwordHasher.Hash("dummy-password-for-timing-parity");
}
