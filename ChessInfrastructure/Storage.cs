using ChessInfrastructure.Entities;
using ChessInfrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChessInfrastructure;

/// <summary>
/// Контекст хранения партий, участников и дерева ходов
/// </summary>
public class Storage : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// Создает контекст с настройками подключения, переданными приложением
    /// </summary>
    /// <param name="options">Настройки контекста и провайдера базы данных</param>
    public Storage(DbContextOptions<Storage> options) : base(options)
    {
    }

    /// <summary>
    /// Сохраненные партии
    /// </summary>
    public DbSet<Game> Games => Set<Game>();

    /// <summary>
    /// Участники партий
    /// </summary>
    public DbSet<Player> Players => Set<Player>();

    /// <summary>
    /// Сохраненные полуходы и их родительские связи
    /// </summary>
    public DbSet<Move> Moves => Set<Move>();

    /// <summary>
    /// Задания разбора сыгранных ходов
    /// </summary>
    public DbSet<MoveAnalysisJob> MoveAnalysisJobs => Set<MoveAnalysisJob>();

    /// <summary>
    /// Задания поиска ответных ходов бота
    /// </summary>
    public DbSet<BotMoveJob> BotMoveJobs => Set<BotMoveJob>();

    /// <summary>
    /// Сохраненные результаты анализа позиций до и после хода
    /// </summary>
    public DbSet<MoveAnalysis> MoveAnalyses => Set<MoveAnalysis>();

    /// <summary>
    /// Настраивает связи аккаунта, партии и результатов заданий с анализами в JSONB
    /// </summary>
    /// <param name="modelBuilder">Построитель модели хранения</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>()
            .HasOne(player => player.User)
            .WithOne()
            .HasForeignKey<Player>(player => player.UserId);

        modelBuilder.Entity<Game>()
            .HasOne(game => game.CurrentMove)
            .WithMany()
            .HasForeignKey(game => game.CurrentMoveId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(game => game.FinalMove)
            .WithMany()
            .HasForeignKey(game => game.FinalMoveId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(game => game.WhitePlayer)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(game => game.BlackPlayer)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Move>()
            .HasOne(move => move.Player)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MoveAnalysisJob>()
            .HasOne(job => job.RecipientPlayer)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MoveAnalysis>()
            .HasOne(result => result.MoveAnalysisJob)
            .WithOne()
            .HasForeignKey<MoveAnalysis>(result => result.MoveAnalysisJobId);

        modelBuilder.Entity<MoveAnalysis>()
            .ComplexProperty(result => result.BeforeAnalysis, analysis => analysis.ToJson());

        modelBuilder.Entity<MoveAnalysis>()
            .ComplexProperty(result => result.AfterAnalysis, analysis => analysis.ToJson());

        modelBuilder.Entity<MoveAnalysisJob>()
            .HasIndex(job => new { job.Status, job.CreatedAtUtc });

        modelBuilder.Entity<BotMoveJob>()
            .HasIndex(job => new { job.Status, job.CreatedAtUtc });
    }
}
