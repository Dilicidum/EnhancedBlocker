namespace EnhancedBlocker.Domain;

/// <summary>Kind of activity captured for time tracking.</summary>
public enum EventType
{
    Navigate,
    Active,
    Idle
}

/// <summary>How a rule pattern is matched against a URL.</summary>
public enum MatchKind
{
    Exact,
    Domain
}

/// <summary>Whether a rule blocks or explicitly allows.</summary>
public enum RuleKind
{
    Block,
    Allow
}

/// <summary>Where a rule came from.</summary>
public enum RuleSource
{
    Manual,
    Derived
}

/// <summary>A user/feedback decision recorded as a label.</summary>
public enum Decision
{
    Allow,
    Block
}

/// <summary>Where a label originated.</summary>
public enum LabelSource
{
    GoodCall,
    BadCall,
    Bounce,
    ActiveQuery
}

/// <summary>
/// Outcome of the decision cascade. <see cref="Pending"/> is an M2 seam for the
/// slow ML path; M1 tiers only ever produce <see cref="Allow"/> or <see cref="Block"/>.
/// </summary>
public enum Outcome
{
    Allow,
    Block,
    Pending
}
