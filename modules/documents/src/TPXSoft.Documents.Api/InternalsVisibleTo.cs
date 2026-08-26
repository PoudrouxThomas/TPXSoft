using System.Runtime.CompilerServices;

// Lets TPXSoft.Documents.UnitTests exercise internal-only Api types directly
// (DocumentErrorMapper, ClaimsPrincipalExtensions) instead of only reachable-through-HTTP
// behavior.
[assembly: InternalsVisibleTo("TPXSoft.Documents.UnitTests")]
