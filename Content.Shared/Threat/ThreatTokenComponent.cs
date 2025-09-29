namespace Content.Shared.Threat;

/// <summary>
/// Supplies threat tokens for an entity. Tokens are arbitrary strings that can be weighted by a threat profile.
/// </summary>
[RegisterComponent]
public sealed partial class ThreatTokenComponent : Component
{
    /// <summary>
    /// Tokens exposed by this entity regardless of context.
    /// </summary>
    [DataField("tokens")]
    public HashSet<string> Tokens { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool AddToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return Tokens.Add(token);
    }

    public bool RemoveToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return Tokens.Remove(token);
    }
}
