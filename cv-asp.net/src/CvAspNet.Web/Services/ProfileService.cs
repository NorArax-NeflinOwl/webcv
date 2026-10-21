using System.Globalization;
using CvAspNet.Web.Data;
using CvAspNet.Web.Models;
using CvAspNet.Web.Repositories;

namespace CvAspNet.Web.Services;

public interface IProfileService
{
    /// <summary>The CV in the given language; texts missing in that language fall back to Polish.</summary>
    /// <exception cref="InvalidOperationException">The database holds no profile.</exception>
    Task<ProfileDto> GetProfileAsync(string language = ProfileService.DefaultLanguage, CancellationToken cancellationToken = default);
}

public sealed class ProfileService(
    IProfileRepository profileRepository,
    IProfileTranslationRepository profileTranslationRepository,
    IProfileSkillRepository profileSkillRepository,
    IProfileStackRepository profileStackRepository,
    IExperienceRepository experienceRepository,
    IExperienceTranslationRepository experienceTranslationRepository) : IProfileService
{
    public const string DefaultLanguage = "pl";

    /// <summary>This backend's rows in profile_translation, profile_skill and profile_stack; cv-spring-kotlin reads "kotlin".</summary>
    public const string Backend = "dotnet";

    public async Task<ProfileDto> GetProfileAsync(string language = DefaultLanguage, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.FindFirstAsync(cancellationToken)
            ?? throw new InvalidOperationException("Profile not found in database");

        // Description, technologies and stack are this backend's own (column backend).
        var translation = await profileTranslationRepository.FindByProfileIdAndBackendAndLanguageAsync(profile.Id, Backend, language, cancellationToken)
            ?? await profileTranslationRepository.FindByProfileIdAndBackendAndLanguageAsync(profile.Id, Backend, DefaultLanguage, cancellationToken);

        // Each collection in its own query, as the Kotlin backend does.
        var skills = await profileSkillRepository.FindByProfileIdAndBackendAsync(profile.Id, Backend, cancellationToken);
        var stacks = await profileStackRepository.FindByProfileIdAndBackendAsync(profile.Id, Backend, cancellationToken);
        var experiences = await experienceRepository.FindByProfileIdOrderByDateFromDescAsync(profile.Id, cancellationToken);
        var experienceTranslations = await FetchExperienceTranslationsAsync(experiences, language, cancellationToken);

        return new ProfileDto(
            Title: $"{profile.Name}  Backend Developer",
            Eyebrow: translation?.Eyebrow ?? "",
            Name: profile.Name,
            Role: translation?.Role ?? "",
            Bio: translation?.Bio ?? "",
            Links: BuildLinks(profile),
            Technologies: BuildTechnologies(skills),
            Stacks: stacks.Select(stack => stack.StackItem.Label).ToList(),
            Footer: translation?.Footer ?? "",
            FooterStatus: profile.FooterStatus.ToString().ToUpperInvariant(),
            Experiences: experiences.Select(experience => ToDto(experience, experienceTranslations.GetValueOrDefault(experience.Id))).ToList());
    }

    private async Task<Dictionary<Guid, ExperienceTranslation>> FetchExperienceTranslationsAsync(
        IReadOnlyList<Experience> experiences, string language, CancellationToken cancellationToken)
    {
        var experienceIds = experiences.Select(experience => experience.Id).ToList();
        var translations = await experienceTranslationRepository.FindByExperienceIdsAndLanguageAsync(experienceIds, language, cancellationToken);

        var byExperience = new Dictionary<Guid, ExperienceTranslation>();
        foreach (var translation in translations)
        {
            byExperience[translation.ExperienceId] = translation;
        }
        return byExperience;
    }

    private static ExperienceDto ToDto(Experience experience, ExperienceTranslation? translation) => new(
        Company: experience.Company,
        Position: translation?.Position ?? experience.Position,
        ContractType: translation?.ContractType ?? experience.ContractType,
        DateFrom: FormatDate(experience.DateFrom),
        DateTo: experience.DateTo is { } dateTo ? FormatDate(dateTo) : null,
        IsCurrent: experience.IsCurrent,
        Description: translation?.Description ?? experience.Description);

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Skill name to tag; a repeated name keeps its first position and its last tag.</summary>
    private static Dictionary<string, string> BuildTechnologies(IReadOnlyList<ProfileSkill> skills)
    {
        var technologies = new Dictionary<string, string>();
        foreach (var profileSkill in skills)
        {
            technologies[profileSkill.Skill.Name] = profileSkill.Tag ?? profileSkill.Skill.Category;
        }
        return technologies;
    }

    private static Dictionary<string, string> BuildLinks(Profile profile)
    {
        var links = new Dictionary<string, string>();
        AddWhenPresent("GithubLink", profile.GithubUrl);
        AddWhenPresent("LinkedIn", profile.LinkedinUrl);
        AddWhenPresent("E-mail", profile.Email);
        return links;

        void AddWhenPresent(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) links[key] = value;
        }
    }
}
