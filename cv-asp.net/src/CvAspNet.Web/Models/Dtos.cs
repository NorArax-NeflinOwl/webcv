namespace CvAspNet.Web.Models;

// The JSON contract of the shared pages (cv-shared/web/js): the same shapes as cv-spring-kotlin's DTOs.

/// <param name="FooterStatus">"GREEN" | "YELLOW" | "RED".</param>
/// <param name="Links">Keys "GithubLink", "LinkedIn", "E-mail"; only the ones the profile has.</param>
/// <param name="Technologies">Skill name to its tag (or its category when untagged), in display order.</param>
public sealed record ProfileDto(
    string Title,
    string Eyebrow,
    string Name,
    string Role,
    string Bio,
    IReadOnlyDictionary<string, string> Links,
    IReadOnlyDictionary<string, string> Technologies,
    IReadOnlyList<string> Stacks,
    string Footer,
    string FooterStatus,
    IReadOnlyList<ExperienceDto> Experiences);

/// <param name="DateFrom">yyyy-MM-dd.</param>
/// <param name="DateTo">yyyy-MM-dd; null while the job lasts.</param>
public sealed record ExperienceDto(
    string Company,
    string Position,
    string? ContractType,
    string DateFrom,
    string? DateTo,
    bool IsCurrent,
    string? Description);

public sealed record PandaGamePlayerScoreRequest(string Nick, int Score);

/// <param name="PlayedAt">Polish local time, ISO-8601 without an offset (as Java's LocalDateTime prints it).</param>
public sealed record PandaGamePlayerScoreResponse(string Nick, int Score, string PlayedAt);

public sealed record ErrorResponse(string Error);

/// <param name="Label">Footer label of the shared CV page: which backend serves it.</param>
public sealed record BackendInfoDto(string Label);
