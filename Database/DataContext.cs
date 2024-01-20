using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Newtonsoft.Json;

namespace Devil7Softwares.TeraboxDownloader.Database;

internal class DataContext : DbContext
{
    private readonly IConfiguration _configuration;

    public DataContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DbSet<Models.ChatEntity> Chats { get; set; }
    public DbSet<Models.ChatConfigEntity> ChatConfigs { get; set; }
    public DbSet<Models.JobEntity> Jobs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_configuration.DatabasePath}");

#pragma warning disable EF1001 // Internal EF Core API usage.
        optionsBuilder.ReplaceService<IMigrationsAssembly, MigrationsAssembly>();
#pragma warning restore EF1001 // Internal EF Core API usage.
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.ChatEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<Models.ChatEntity>().HasOne(x => x.Config).WithOne(x => x!.Chat).HasForeignKey<Models.ChatConfigEntity>(x => x.ChatId);

        modelBuilder.Entity<Models.ChatConfigEntity>().HasKey(x => x.Id);

        modelBuilder.Entity<Models.JobEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<Models.JobEntity>().HasOne(x => x.Chat).WithMany(x => x.Jobs).HasForeignKey(x => x.ChatId);
        modelBuilder.Entity<Models.JobEntity>().Property(x => x.DownloadedFiles).HasConversion(
            x => JsonConvert.SerializeObject(x),
            x => JsonConvert.DeserializeObject<List<Downloader.DownloadedFile>>(x) ?? new()
        );
    }
}
