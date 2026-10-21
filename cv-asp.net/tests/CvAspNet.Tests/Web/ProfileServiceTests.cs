using CvAspNet.Web.Data;
using CvAspNet.Web.Repositories;
using CvAspNet.Web.Services;
using NSubstitute;

namespace CvAspNet.Tests.Web;

public class ProfileServiceTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid ExperienceId = Guid.NewGuid();

    private readonly IProfileRepository profileRepository = Substitute.For<IProfileRepository>();
    private readonly IProfileTranslationRepository profileTranslationRepository = Substitute.For<IProfileTranslationRepository>();
    private readonly IProfileSkillRepository profileSkillRepository = Substitute.For<IProfileSkillRepository>();
    private readonly IProfileStackRepository profileStackRepository = Substitute.For<IProfileStackRepository>();
    private readonly IExperienceRepository experienceRepository = Substitute.For<IExperienceRepository>();
    private readonly IExperienceTranslationRepository experienceTranslationRepository = Substitute.For<IExperienceTranslationRepository>();
    private readonly ProfileService profileService;

    public ProfileServiceTests()
    {
        profileService = new ProfileService(
            profileRepository, profileTranslationRepository, profileSkillRepository,
            profileStackRepository, experienceRepository, experienceTranslationRepository);

        GivenProfile(DefaultProfile());
        GivenTranslation("pl", new ProfileTranslation { ProfileId = ProfileId, Language = "pl", Eyebrow = "backend developer", Role = "<span>Kotlin</span>", Bio = "Test bio", Footer = "dostępny" });
        GivenTranslation("en", new ProfileTranslation { ProfileId = ProfileId, Language = "en", Eyebrow = "backend developer", Role = "<span>Kotlin</span>", Bio = "Test bio EN", Footer = "available" });
        profileSkillRepository.FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>()).Returns(
        [
            new ProfileSkill { ProfileId = ProfileId, Skill = new Skill { Name = "Kotlin", Category = "primary language" }, Tag = "primary language", SortOrder = 1 },
            new ProfileSkill { ProfileId = ProfileId, Skill = new Skill { Name = "Spring Boot", Category = "web / rest api" }, Tag = "web / rest api", SortOrder = 2 },
        ]);
        profileStackRepository.FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>()).Returns(
        [
            new ProfileStack { ProfileId = ProfileId, StackItem = new StackItem { Label = "JVM 17", SortOrder = 1 }, SortOrder = 1 },
            new ProfileStack { ProfileId = ProfileId, StackItem = new StackItem { Label = "Docker", SortOrder = 2 }, SortOrder = 2 },
        ]);
        GivenExperiences(DefaultExperience());
        GivenExperienceTranslations("pl", new ExperienceTranslation { ExperienceId = ExperienceId, Language = "pl", Position = "Developer PL", ContractType = "B2B", Description = "Description PL" });
        GivenExperienceTranslations("en", new ExperienceTranslation { ExperienceId = ExperienceId, Language = "en", Position = "Developer EN", ContractType = "B2B", Description = "Description EN" });
    }

    private static Profile DefaultProfile() => new()
    {
        Id = ProfileId,
        Name = "Jan Kowalski",
        Eyebrow = "backend developer",
        Role = "<span>Kotlin</span>",
        Bio = "Test bio",
        Footer = "available",
        FooterStatus = FooterStatus.Yellow,
        Email = "test@example.com",
        GithubUrl = "https://github.com/test",
        LinkedinUrl = "https://linkedin.com/in/test",
    };

    private static Experience DefaultExperience() => new()
    {
        Id = ExperienceId,
        ProfileId = ProfileId,
        Company = "Test Company Ltd.",
        Position = "Developer PL",
        ContractType = "B2B",
        DateFrom = new DateOnly(2023, 1, 1),
        IsCurrent = true,
        Description = "Description PL",
    };

    private void GivenProfile(Profile? profile) =>
        profileRepository.FindFirstAsync(Arg.Any<CancellationToken>()).Returns(profile);

    private void GivenTranslation(string language, ProfileTranslation? translation) =>
        profileTranslationRepository.FindByProfileIdAndBackendAndLanguageAsync(ProfileId, "dotnet", language, Arg.Any<CancellationToken>()).Returns(translation);

    private void GivenExperiences(params Experience[] experiences) =>
        experienceRepository.FindByProfileIdOrderByDateFromDescAsync(ProfileId, Arg.Any<CancellationToken>()).Returns(experiences);

    private void GivenExperienceTranslations(string language, params ExperienceTranslation[] translations) =>
        experienceTranslationRepository.FindByExperienceIdsAndLanguageAsync(Arg.Any<IReadOnlyCollection<Guid>>(), language, Arg.Any<CancellationToken>())
            .Returns(translations);

    // ── Basic data ──

    [Fact]
    public async Task GetProfile_ReturnsCorrectBasicData()
    {
        var result = await profileService.GetProfileAsync();

        Assert.Equal("Jan Kowalski  Backend Developer", result.Title);
        Assert.Equal("backend developer", result.Eyebrow);
        Assert.Equal("Jan Kowalski", result.Name);
        Assert.Equal("<span>Kotlin</span>", result.Role);
        Assert.Equal("Test bio", result.Bio);
        Assert.Equal("dostępny", result.Footer);
    }

    // ── Footer status ──

    [Theory]
    [InlineData(FooterStatus.Yellow, "YELLOW")]
    [InlineData(FooterStatus.Green, "GREEN")]
    [InlineData(FooterStatus.Red, "RED")]
    public async Task GetProfile_ReturnsFooterStatusInUpperCase(FooterStatus status, string expected)
    {
        var profile = DefaultProfile();
        profile.FooterStatus = status;
        GivenProfile(profile);

        Assert.Equal(expected, (await profileService.GetProfileAsync()).FooterStatus);
    }

    // ── Links ──

    [Fact]
    public async Task GetProfile_ReturnsCorrectLinks()
    {
        var links = (await profileService.GetProfileAsync()).Links;

        Assert.Equal("https://github.com/test", links["GithubLink"]);
        Assert.Equal("https://linkedin.com/in/test", links["LinkedIn"]);
        Assert.Equal("test@example.com", links["E-mail"]);
    }

    [Fact]
    public async Task GetProfile_SkipsNullAndEmptyLinks()
    {
        var profile = DefaultProfile();
        profile.GithubUrl = null;
        profile.LinkedinUrl = "";
        GivenProfile(profile);

        var links = (await profileService.GetProfileAsync()).Links;

        Assert.False(links.ContainsKey("GithubLink"));
        Assert.False(links.ContainsKey("LinkedIn"));
        Assert.True(links.ContainsKey("E-mail"));
    }

    [Fact]
    public async Task GetProfile_ReturnsNoLinksWhenThereIsNoContactData()
    {
        var profile = DefaultProfile();
        profile.GithubUrl = profile.LinkedinUrl = profile.Email = null;
        GivenProfile(profile);

        Assert.Empty((await profileService.GetProfileAsync()).Links);
    }

    // ── Technologies and stack ──

    [Fact]
    public async Task GetProfile_ReturnsTechnologiesInOrder()
    {
        var technologies = (await profileService.GetProfileAsync()).Technologies;

        Assert.Equal(["Kotlin", "Spring Boot"], technologies.Keys);
        Assert.Equal("primary language", technologies["Kotlin"]);
        Assert.Equal("web / rest api", technologies["Spring Boot"]);
    }

    [Fact]
    public async Task GetProfile_UsesTheCategoryOfAnUntaggedSkill()
    {
        profileSkillRepository.FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>()).Returns(
            [new ProfileSkill { Skill = new Skill { Name = "Docker", Category = "containerization" }, Tag = null }]);

        Assert.Equal("containerization", (await profileService.GetProfileAsync()).Technologies["Docker"]);
    }

    [Fact]
    public async Task GetProfile_ReturnsCorrectStack()
    {
        Assert.Equal(["JVM 17", "Docker"], (await profileService.GetProfileAsync()).Stacks);
    }

    // ── Language ──

    [Fact]
    public async Task GetProfile_ReturnsEnTranslationWhenLangIsEn()
    {
        var result = await profileService.GetProfileAsync("en");

        Assert.Equal("Test bio EN", result.Bio);
        Assert.Equal("available", result.Footer);
    }

    [Fact]
    public async Task GetProfile_FallsBackToPlWhenTheLanguageHasNoTranslation()
    {
        GivenTranslation("de", null);
        GivenExperienceTranslations("de");

        Assert.Equal("dostępny", (await profileService.GetProfileAsync("de")).Footer);
    }

    // ── Null values ──

    [Fact]
    public async Task GetProfile_ReturnsEmptyStringsWhenTranslationFieldsAreNull()
    {
        GivenTranslation("pl", new ProfileTranslation { ProfileId = ProfileId, Language = "pl" });

        var result = await profileService.GetProfileAsync();

        Assert.Equal("", result.Eyebrow);
        Assert.Equal("", result.Role);
        Assert.Equal("", result.Bio);
        Assert.Equal("", result.Footer);
    }

    // ── Experience ──

    [Fact]
    public async Task GetProfile_ReturnsExperienceWithPlTranslation()
    {
        var experience = Assert.Single((await profileService.GetProfileAsync()).Experiences);

        Assert.Equal("Test Company Ltd.", experience.Company);
        Assert.Equal("Developer PL", experience.Position);
        Assert.Equal("B2B", experience.ContractType);
        Assert.Equal("2023-01-01", experience.DateFrom);
        Assert.Null(experience.DateTo);
        Assert.True(experience.IsCurrent);
        Assert.Equal("Description PL", experience.Description);
    }

    [Fact]
    public async Task GetProfile_ReturnsExperienceWithEnTranslation()
    {
        var experience = (await profileService.GetProfileAsync("en")).Experiences[0];

        Assert.Equal("Developer EN", experience.Position);
        Assert.Equal("Description EN", experience.Description);
    }

    [Fact]
    public async Task GetProfile_ExperienceFallsBackToItsOwnTextsWithoutTranslation()
    {
        GivenExperienceTranslations("pl");

        Assert.Equal("Developer PL", (await profileService.GetProfileAsync()).Experiences[0].Position);
    }

    [Fact]
    public async Task GetProfile_ExperienceDateToIsSetWhenTheJobHasEnded()
    {
        var experience = DefaultExperience();
        experience.IsCurrent = false;
        experience.DateTo = new DateOnly(2024, 6, 30);
        GivenExperiences(experience);

        var result = (await profileService.GetProfileAsync()).Experiences[0];

        Assert.Equal("2024-06-30", result.DateTo);
        Assert.False(result.IsCurrent);
    }

    // ── Errors and empty collections ──

    [Fact]
    public async Task GetProfile_ThrowsWhenThereIsNoProfile()
    {
        GivenProfile(null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => profileService.GetProfileAsync());
        Assert.Equal("Profile not found in database", ex.Message);
    }

    [Fact]
    public async Task GetProfile_ReturnsEmptyTechnologiesWhenThereAreNoSkills()
    {
        profileSkillRepository.FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>()).Returns([]);

        Assert.Empty((await profileService.GetProfileAsync()).Technologies);
    }

    [Fact]
    public async Task GetProfile_ReturnsEmptyStacksWhenThereAreNoStackItems()
    {
        profileStackRepository.FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>()).Returns([]);

        Assert.Empty((await profileService.GetProfileAsync()).Stacks);
    }

    [Fact]
    public async Task GetProfile_ReturnsEmptyExperiencesWhenThereAreNone()
    {
        GivenExperiences();
        GivenExperienceTranslations("pl");

        Assert.Empty((await profileService.GetProfileAsync()).Experiences);
    }

    // ── Call verification ──

    [Fact]
    public async Task GetProfile_CallsEveryRepositoryExactlyOnce()
    {
        await profileService.GetProfileAsync();

        await profileRepository.Received(1).FindFirstAsync(Arg.Any<CancellationToken>());
        await profileTranslationRepository.Received(1).FindByProfileIdAndBackendAndLanguageAsync(ProfileId, "dotnet", "pl", Arg.Any<CancellationToken>());
        await profileSkillRepository.Received(1).FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>());
        await profileStackRepository.Received(1).FindByProfileIdAndBackendAsync(ProfileId, "dotnet", Arg.Any<CancellationToken>());
        await experienceRepository.Received(1).FindByProfileIdOrderByDateFromDescAsync(ProfileId, Arg.Any<CancellationToken>());
        await experienceTranslationRepository.Received(1).FindByExperienceIdsAndLanguageAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { ExperienceId })), "pl", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProfile_CallsNoOtherRepositoryWhenTheProfileIsMissing()
    {
        GivenProfile(null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => profileService.GetProfileAsync());

        await profileTranslationRepository.DidNotReceiveWithAnyArgs().FindByProfileIdAndBackendAndLanguageAsync(default, default!, default!, default);
        await profileSkillRepository.DidNotReceiveWithAnyArgs().FindByProfileIdAndBackendAsync(default, default!, default);
        await profileStackRepository.DidNotReceiveWithAnyArgs().FindByProfileIdAndBackendAsync(default, default!, default);
        await experienceRepository.DidNotReceiveWithAnyArgs().FindByProfileIdOrderByDateFromDescAsync(default, default);
    }
}
