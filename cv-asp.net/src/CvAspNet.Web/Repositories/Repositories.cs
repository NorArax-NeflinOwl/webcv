using CvAspNet.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace CvAspNet.Web.Repositories;

// The queries the services need, one interface per aggregate, so that the services can be tested without a database.

public interface IProfileRepository
{
    /// <summary>The CV's single profile, or null when the database holds none.</summary>
    Task<Profile?> FindFirstAsync(CancellationToken cancellationToken = default);
}

public interface IProfileTranslationRepository
{
    Task<ProfileTranslation?> FindByProfileIdAndBackendAndLanguageAsync(Guid profileId, string backend, string language, CancellationToken cancellationToken = default);
}

public interface IProfileSkillRepository
{
    /// <summary>The profile's skills for the given backend (skill included), in display order.</summary>
    Task<IReadOnlyList<ProfileSkill>> FindByProfileIdAndBackendAsync(Guid profileId, string backend, CancellationToken cancellationToken = default);
}

public interface IProfileStackRepository
{
    /// <summary>The profile's stack items for the given backend (item included), in display order.</summary>
    Task<IReadOnlyList<ProfileStack>> FindByProfileIdAndBackendAsync(Guid profileId, string backend, CancellationToken cancellationToken = default);
}

public interface IExperienceRepository
{
    Task<IReadOnlyList<Experience>> FindByProfileIdOrderByDateFromDescAsync(Guid profileId, CancellationToken cancellationToken = default);
}

public interface IExperienceTranslationRepository
{
    Task<IReadOnlyList<ExperienceTranslation>> FindByExperienceIdsAndLanguageAsync(
        IReadOnlyCollection<Guid> experienceIds, string language, CancellationToken cancellationToken = default);
}

public interface IPandaGamePlayerScoreRepository
{
    Task<PandaGamePlayerScore> SaveAsync(PandaGamePlayerScore score, CancellationToken cancellationToken = default);

    /// <summary>The best scores, highest first; on equal scores the earlier one first.</summary>
    Task<IReadOnlyList<PandaGamePlayerScore>> FindTopAsync(int count, CancellationToken cancellationToken = default);
}

internal sealed class ProfileRepository(CvDbContext db) : IProfileRepository
{
    public Task<Profile?> FindFirstAsync(CancellationToken cancellationToken = default) =>
        db.Profiles.AsNoTracking().OrderBy(profile => profile.Name).FirstOrDefaultAsync(cancellationToken);
}

internal sealed class ProfileTranslationRepository(CvDbContext db) : IProfileTranslationRepository
{
    public Task<ProfileTranslation?> FindByProfileIdAndBackendAndLanguageAsync(
        Guid profileId, string backend, string language, CancellationToken cancellationToken = default) =>
        db.ProfileTranslations.AsNoTracking()
            .FirstOrDefaultAsync(
                translation => translation.ProfileId == profileId && translation.Backend == backend && translation.Language == language,
                cancellationToken);
}

internal sealed class ProfileSkillRepository(CvDbContext db) : IProfileSkillRepository
{
    public async Task<IReadOnlyList<ProfileSkill>> FindByProfileIdAndBackendAsync(Guid profileId, string backend, CancellationToken cancellationToken = default) =>
        await db.ProfileSkills.AsNoTracking()
            .Include(profileSkill => profileSkill.Skill)
            .Where(profileSkill => profileSkill.ProfileId == profileId && profileSkill.Backend == backend)
            .OrderBy(profileSkill => profileSkill.SortOrder)
            .ToListAsync(cancellationToken);
}

internal sealed class ProfileStackRepository(CvDbContext db) : IProfileStackRepository
{
    public async Task<IReadOnlyList<ProfileStack>> FindByProfileIdAndBackendAsync(Guid profileId, string backend, CancellationToken cancellationToken = default) =>
        await db.ProfileStacks.AsNoTracking()
            .Include(profileStack => profileStack.StackItem)
            .Where(profileStack => profileStack.ProfileId == profileId && profileStack.Backend == backend)
            .OrderBy(profileStack => profileStack.SortOrder)
            .ToListAsync(cancellationToken);
}

internal sealed class ExperienceRepository(CvDbContext db) : IExperienceRepository
{
    public async Task<IReadOnlyList<Experience>> FindByProfileIdOrderByDateFromDescAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        await db.Experiences.AsNoTracking()
            .Where(experience => experience.ProfileId == profileId)
            .OrderByDescending(experience => experience.DateFrom)
            .ToListAsync(cancellationToken);
}

internal sealed class ExperienceTranslationRepository(CvDbContext db) : IExperienceTranslationRepository
{
    public async Task<IReadOnlyList<ExperienceTranslation>> FindByExperienceIdsAndLanguageAsync(
        IReadOnlyCollection<Guid> experienceIds, string language, CancellationToken cancellationToken = default) =>
        await db.ExperienceTranslations.AsNoTracking()
            .Where(translation => experienceIds.Contains(translation.ExperienceId) && translation.Language == language)
            .ToListAsync(cancellationToken);
}

internal sealed class PandaGamePlayerScoreRepository(CvDbContext db) : IPandaGamePlayerScoreRepository
{
    public async Task<PandaGamePlayerScore> SaveAsync(PandaGamePlayerScore score, CancellationToken cancellationToken = default)
    {
        db.PandaGamePlayerScores.Add(score);
        await db.SaveChangesAsync(cancellationToken);
        return score;
    }

    public async Task<IReadOnlyList<PandaGamePlayerScore>> FindTopAsync(int count, CancellationToken cancellationToken = default) =>
        await db.PandaGamePlayerScores.AsNoTracking()
            .OrderByDescending(score => score.Score)
            .ThenBy(score => score.PlayedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
}
