
namespace UAI.ZeroVP.Data.Abstractions;

/// <summary>
/// Raised when an update uses an outdated todo version.
/// </summary>
public sealed class TodoConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new concurrency conflict exception.
    /// </summary>
    /// <param name="id">Identifier of the conflicting todo item.</param>
    /// <param name="expectedVersion">Version provided by the client.</param>
    /// <param name="actualVersion">Current version stored in the repository.</param>
    public TodoConcurrencyException(
        Guid id,
        long expectedVersion,
        long actualVersion)
        : base(
            $"Todo {id} version conflict: expected version " +
            $"{expectedVersion}, current version {actualVersion}.")
    {
    }
}
