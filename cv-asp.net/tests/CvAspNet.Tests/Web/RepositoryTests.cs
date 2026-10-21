using CvAspNet.Web.Data;
using CvAspNet.Web.Repositories;

namespace CvAspNet.Tests.Web;

public sealed class RepositoryTests : IDisposable
{
    private readonly TestDatabase database = new();
    private readonly Profile savedProfile;

    public RepositoryTests()
    {
        using var db = database.CreateContext();
        savedProfile = new Profile { Name = "Jan Kowalski", Eyebrow = "backend dev", Email = "jan@example.com", GithubUrl = "https://github.com/test" };
        var skill = new Skill { Name = "C#", Category = "primary language" };
        var kotlinSkill = new Skill { Name = "Kotlin", Category = "primary language" };
        var stackItem = new StackItem { Label = ".NET 10", SortOrder = 1 };
        var kotlinStackItem = new StackItem { Label = "JVM 17", SortOrder = 1 };
        db.AddRange(savedProfile, skill, kotlinSkill, stackItem, kotlinStackItem);
        db.AddRange(
            new ProfileSkill { ProfileId = savedProfile.Id, Backend = "dotnet", Skill = skill, Tag = "primary language", SortOrder = 1 },
            new ProfileSkill { ProfileId = savedProfile.Id, Backend = "kotlin", Skill = kotlinSkill, Tag = "primary language", SortOrder = 1 },
            new ProfileStack { ProfileId = savedProfile.Id, Backend = "dotnet", StackItem = stackItem, SortOrder = 1 },
            new ProfileStack { ProfileId = savedProfile.Id, Backend = "kotlin", StackItem = kotlinStackItem, SortOrder = 1 },
            new Experience
            {
                ProfileId = savedProfile.Id, Company = "Firma Sp. z o.o.", Position = "Developer", ContractType = "B2B",
                DateFrom = new DateOnly(2023, 1, 1), IsCurrent = true,
            },
            new ProfileTranslation { ProfileId = savedProfile.Id, Backend = "dotnet", Language = "en", Bio = "Bio EN" },
            new ProfileTranslation { ProfileId = savedProfile.Id, Backend = "kotlin", Language = "en", Bio = "Bio EN Kotlin" });
        db.SaveChanges();
    }

    public void Dispose() => database.Dispose();

    private Guid AddOtherProfile()
    {
        using var db = database.CreateContext();
        var other = new Profile { Name = "Inny" };
        db.Profiles.Add(other);
        db.SaveChanges();
        return other.Id;
    }

    // ── ProfileRepository ──

    [Fact]
    public async Task FindFirst_ReturnsTheProfile()
    {
        await using var db = database.CreateContext();

        var result = await new ProfileRepository(db).FindFirstAsync();

        Assert.NotNull(result);
        Assert.Equal("Jan Kowalski", result.Name);
        Assert.Equal("backend dev", result.Eyebrow);
    }

    [Fact]
    public async Task FindFirst_ReturnsNullWhenThereIsNoProfile()
    {
        await using var db = database.CreateContext();
        db.RemoveRange(db.ProfileSkills);
        db.RemoveRange(db.ProfileStacks);
        db.RemoveRange(db.Experiences);
        db.RemoveRange(db.ProfileTranslations);
        db.RemoveRange(db.Profiles);
        await db.SaveChangesAsync();

        Assert.Null(await new ProfileRepository(db).FindFirstAsync());
    }

    // ── ProfileTranslationRepository ──

    [Fact]
    public async Task FindByProfileIdAndBackendAndLanguage_ReturnsTheTranslationOfThatBackend()
    {
        await using var db = database.CreateContext();
        var repository = new ProfileTranslationRepository(db);

        Assert.Equal("Bio EN", (await repository.FindByProfileIdAndBackendAndLanguageAsync(savedProfile.Id, "dotnet", "en"))?.Bio);
        Assert.Equal("Bio EN Kotlin", (await repository.FindByProfileIdAndBackendAndLanguageAsync(savedProfile.Id, "kotlin", "en"))?.Bio);
        Assert.Null(await repository.FindByProfileIdAndBackendAndLanguageAsync(savedProfile.Id, "dotnet", "de"));
    }

    // ── ProfileSkillRepository ──

    [Fact]
    public async Task FindSkillsByProfileId_ReturnsTheProfileSkillsWithTheSkill()
    {
        await using var db = database.CreateContext();

        var result = Assert.Single(await new ProfileSkillRepository(db).FindByProfileIdAndBackendAsync(savedProfile.Id, "dotnet"));

        Assert.Equal("C#", result.Skill.Name);
        Assert.Equal("primary language", result.Tag);
    }

    [Fact]
    public async Task FindSkillsByProfileId_ReturnsTheOtherBackendsSkillsSeparately()
    {
        await using var db = database.CreateContext();

        var result = Assert.Single(await new ProfileSkillRepository(db).FindByProfileIdAndBackendAsync(savedProfile.Id, "kotlin"));

        Assert.Equal("Kotlin", result.Skill.Name);
    }

    [Fact]
    public async Task FindSkillsByProfileId_ReturnsNothingForAnotherProfile()
    {
        var otherProfileId = AddOtherProfile();
        await using var db = database.CreateContext();

        Assert.Empty(await new ProfileSkillRepository(db).FindByProfileIdAndBackendAsync(otherProfileId, "dotnet"));
    }

    // ── ProfileStackRepository ──

    [Fact]
    public async Task FindStacksByProfileId_ReturnsTheStackItems()
    {
        await using var db = database.CreateContext();

        var result = Assert.Single(await new ProfileStackRepository(db).FindByProfileIdAndBackendAsync(savedProfile.Id, "dotnet"));

        Assert.Equal(".NET 10", result.StackItem.Label);
    }

    [Fact]
    public async Task FindStacksByProfileId_ReturnsTheOtherBackendsStackItemsSeparately()
    {
        await using var db = database.CreateContext();

        var result = Assert.Single(await new ProfileStackRepository(db).FindByProfileIdAndBackendAsync(savedProfile.Id, "kotlin"));

        Assert.Equal("JVM 17", result.StackItem.Label);
    }

    // ── ExperienceRepository ──

    [Fact]
    public async Task FindExperiences_ReturnsTheExperience()
    {
        await using var db = database.CreateContext();

        var result = Assert.Single(await new ExperienceRepository(db).FindByProfileIdOrderByDateFromDescAsync(savedProfile.Id));

        Assert.Equal("Firma Sp. z o.o.", result.Company);
        Assert.Equal("B2B", result.ContractType);
        Assert.True(result.IsCurrent);
    }

    [Fact]
    public async Task FindExperiences_SortsByDateFromDescending()
    {
        await using (var setup = database.CreateContext())
        {
            setup.Experiences.Add(new Experience { ProfileId = savedProfile.Id, Company = "Starsza Firma", Position = "Junior", DateFrom = new DateOnly(2020, 1, 1) });
            await setup.SaveChangesAsync();
        }
        await using var db = database.CreateContext();

        var result = await new ExperienceRepository(db).FindByProfileIdOrderByDateFromDescAsync(savedProfile.Id);

        Assert.Equal(["Firma Sp. z o.o.", "Starsza Firma"], result.Select(experience => experience.Company));
    }

    [Fact]
    public async Task FindExperiences_ReturnsNothingForAnotherProfile()
    {
        var otherProfileId = AddOtherProfile();
        await using var db = database.CreateContext();

        Assert.Empty(await new ExperienceRepository(db).FindByProfileIdOrderByDateFromDescAsync(otherProfileId));
    }

    // ── ExperienceTranslationRepository ──

    [Fact]
    public async Task FindExperienceTranslations_ReturnsOnlyTheGivenExperiencesInTheGivenLanguage()
    {
        await using (var setup = database.CreateContext())
        {
            var experienceId = setup.Experiences.Single().Id;
            setup.ExperienceTranslations.AddRange(
                new ExperienceTranslation { ExperienceId = experienceId, Language = "en", Position = "Developer EN" },
                new ExperienceTranslation { ExperienceId = experienceId, Language = "pl", Position = "Developer PL" });
            await setup.SaveChangesAsync();
        }
        await using var db = database.CreateContext();
        var repository = new ExperienceTranslationRepository(db);
        var ids = db.Experiences.Select(experience => experience.Id).ToList();

        var result = Assert.Single(await repository.FindByExperienceIdsAndLanguageAsync(ids, "en"));

        Assert.Equal("Developer EN", result.Position);
        Assert.Empty(await repository.FindByExperienceIdsAndLanguageAsync([], "en"));
    }
}

public sealed class PandaGamePlayerScoreRepositoryTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    private void Save(params PandaGamePlayerScore[] scores)
    {
        using var db = database.CreateContext();
        db.PandaGamePlayerScores.AddRange(scores);
        db.SaveChanges();
    }

    private async Task<IReadOnlyList<PandaGamePlayerScore>> FindTopAsync()
    {
        await using var db = database.CreateContext();
        return await new PandaGamePlayerScoreRepository(db).FindTopAsync(10);
    }

    [Fact]
    public async Task FindTop_ReturnsScoresHighestFirst()
    {
        var now = DateTime.Now;
        Save(
            new PandaGamePlayerScore { Nick = "Niski", Score = 50, PlayedAt = now },
            new PandaGamePlayerScore { Nick = "Wysoki", Score = 300, PlayedAt = now },
            new PandaGamePlayerScore { Nick = "Sredni", Score = 150, PlayedAt = now });

        Assert.Equal(["Wysoki", "Sredni", "Niski"], (await FindTopAsync()).Select(score => score.Nick));
    }

    [Fact]
    public async Task FindTop_ReturnsAtMostTen()
    {
        var now = DateTime.Now;
        Save(Enumerable.Range(0, 15).Select(i => new PandaGamePlayerScore { Nick = $"Gracz{i}", Score = i, PlayedAt = now }).ToArray());

        var result = await FindTopAsync();

        Assert.Equal(10, result.Count);
        Assert.Equal(14, result[0].Score);
    }

    [Fact]
    public async Task FindTop_PutsTheEarlierOfEqualScoresFirst()
    {
        var now = DateTime.Now;
        Save(
            new PandaGamePlayerScore { Nick = "Pozniej", Score = 100, PlayedAt = now },
            new PandaGamePlayerScore { Nick = "Wczesniej", Score = 100, PlayedAt = now.AddHours(-1) });

        Assert.Equal(["Wczesniej", "Pozniej"], (await FindTopAsync()).Select(score => score.Nick));
    }

    [Fact]
    public async Task FindTop_ReturnsAnEmptyListWhenThereAreNoScores()
    {
        Assert.Empty(await FindTopAsync());
    }

    [Fact]
    public async Task Save_AssignsAnId()
    {
        await using var db = database.CreateContext();

        var saved = await new PandaGamePlayerScoreRepository(db).SaveAsync(new PandaGamePlayerScore { Nick = "Gracz", Score = 1, PlayedAt = DateTime.Now });

        Assert.NotEqual(Guid.Empty, saved.Id);
    }
}
