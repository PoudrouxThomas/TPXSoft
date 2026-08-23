using System.Runtime.CompilerServices;

// Lets TPXSoft.Auth.UnitTests exercise internal-only Api types directly (AuthErrorMapper,
// ClaimsPrincipalExtensions) instead of only reachable-through-HTTP behavior.
[assembly: InternalsVisibleTo("TPXSoft.Auth.UnitTests")]
