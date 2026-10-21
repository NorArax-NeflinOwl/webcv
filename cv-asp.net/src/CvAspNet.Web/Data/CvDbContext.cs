using Microsoft.EntityFrameworkCore;

namespace CvAspNet.Web.Data;

/// <summary>
/// The schema is owned by the shared SQL migrations (cv-shared/db/migration), not by EF Core:
/// this model only maps onto it — tables and columns in snake_case.
/// </summary>
public sealed class CvDbContext(DbContextOptions<CvDbContext> options) : DbContext(options)
{
    public const string FooterStatusEnumName = "footer_status_enum";

    public DbSet<Icon> Icons => Set<Icon>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<StackItem> StackItems => Set<StackItem>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<ProfileTranslation> ProfileTranslations => Set<ProfileTranslation>();

    public DbSet<ProfileSkill> ProfileSkills => Set<ProfileSkill>();

    public DbSet<ProfileStack> ProfileStacks => Set<ProfileStack>();

    public DbSet<Experience> Experiences => Set<Experience>();

    public DbSet<ExperienceTranslation> ExperienceTranslations => Set<ExperienceTranslation>();

    public DbSet<PandaGamePlayerScore> PandaGamePlayerScores => Set<PandaGamePlayerScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Icon>().ToTable("icon").HasIndex(icon => icon.Key).IsUnique();
        modelBuilder.Entity<Skill>().ToTable("skill");
        modelBuilder.Entity<StackItem>().ToTable("stack_item");
        modelBuilder.Entity<Profile>().ToTable("profile");
        modelBuilder.Entity<ProfileTranslation>().ToTable("profile_translation");
        modelBuilder.Entity<ProfileSkill>().ToTable("profile_skill");
        modelBuilder.Entity<ProfileStack>().ToTable("profile_stack");
        modelBuilder.Entity<Experience>().ToTable("experience");
        modelBuilder.Entity<ExperienceTranslation>().ToTable("experience_translation");
        modelBuilder.Entity<PandaGamePlayerScore>().ToTable("panda_game_player_score");

        // Only PostgreSQL has the enum type (mapped in Program.cs) and the local-time timestamp;
        // other providers (the tests' SQLite) get plain columns.
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<PandaGamePlayerScore>().Property(score => score.PlayedAt).HasColumnType("timestamp without time zone");
        }
        else
        {
            modelBuilder.Entity<Profile>().Property(profile => profile.FooterStatus).HasConversion<string>();
        }
    }
}
