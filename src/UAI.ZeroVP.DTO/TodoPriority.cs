namespace UAI.ZeroVP.DTO;

/// <summary>
/// Todo item priority. Shared enum used by both the API contract and the data layer.
/// </summary>
public enum TodoPriority
{
    /// <summary>Low priority.</summary>
    Low = 0,

    /// <summary>Normal priority (default).</summary>
    Normal = 1,

    /// <summary>High priority.</summary>
    High = 2
}