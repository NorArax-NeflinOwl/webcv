using NpgsqlTypes;

namespace CvAspNet.Web.Data;

/// <summary>Availability shown by the footer dot; stored as the PostgreSQL enum footer_status_enum.</summary>
public enum FooterStatus
{
    [PgName("GREEN")] Green,
    [PgName("YELLOW")] Yellow,
    [PgName("RED")] Red,
}

public sealed class Icon
{
    public Guid Id { get; set; }

    public string Key { get; set; } = "";

    public string SvgPath { get; set; } = "";

    public string? Label { get; set; }
}

public sealed class Skill
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Category { get; set; } = "";

    public Guid? IconId { get; set; }

    public Icon? Icon { get; set; }
}

public sealed class StackItem
{
    public Guid Id { get; set; }

    public string Label { get; set; } = "";

    public int SortOrder { get; set; }

    public Guid? IconId { get; set; }

    public Icon? Icon { get; set; }
}

public sealed class Profile
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? Eyebrow { get; set; }

    public string? Role { get; set; }

    public string? Bio { get; set; }

    public string? Footer { get; set; }

    public FooterStatus FooterStatus { get; set; } = FooterStatus.Red;

    public string? Email { get; set; }

    public string? GithubUrl { get; set; }

    public string? LinkedinUrl { get; set; }
}

/// <summary>The language-dependent texts of a profile; "pl" is the fallback language.</summary>
public sealed class ProfileTranslation
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public Profile? Profile { get; set; }

    /// <summary>The backend whose description this is: "kotlin" or "dotnet".</summary>
    public string Backend { get; set; } = "";

    public string Language { get; set; } = "";

    public string? Eyebrow { get; set; }

    public string? Role { get; set; }

    public string? Bio { get; set; }

    public string? Footer { get; set; }
}

public sealed class ProfileSkill
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public Profile? Profile { get; set; }

    /// <summary>The backend whose technologies this is: "kotlin" or "dotnet".</summary>
    public string Backend { get; set; } = "";

    public Guid SkillId { get; set; }

    public Skill Skill { get; set; } = null!;

    public string? Tag { get; set; }

    public int SortOrder { get; set; }
}

public sealed class ProfileStack
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public Profile? Profile { get; set; }

    /// <summary>The backend whose stack this is: "kotlin" or "dotnet".</summary>
    public string Backend { get; set; } = "";

    public Guid StackItemId { get; set; }

    public StackItem StackItem { get; set; } = null!;

    public int SortOrder { get; set; }
}

public sealed class Experience
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public Profile? Profile { get; set; }

    public string Company { get; set; } = "";

    public string Position { get; set; } = "";

    public string? ContractType { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public bool IsCurrent { get; set; }

    public string? Description { get; set; }
}

/// <summary>The language-dependent texts of an experience entry.</summary>
public sealed class ExperienceTranslation
{
    public Guid Id { get; set; }

    public Guid ExperienceId { get; set; }

    public Experience? Experience { get; set; }

    public string Language { get; set; } = "";

    public string Position { get; set; } = "";

    public string? ContractType { get; set; }

    public string? Description { get; set; }
}

public sealed class PandaGamePlayerScore
{
    public Guid Id { get; set; }

    public string Nick { get; set; } = "";

    public int Score { get; set; }

    /// <summary>Polish local time (timestamp without time zone), as the Kotlin backend stores it.</summary>
    public DateTime PlayedAt { get; set; }
}
