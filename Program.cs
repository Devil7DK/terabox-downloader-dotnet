using Devil7Softwares.TeraboxDownloader.Database;
using Devil7Softwares.TeraboxDownloader.Terabox;
using Devil7Softwares.TeraboxDownloader.Enums;
using Devil7Softwares.TeraboxDownloader.Jobs;
using Devil7Softwares.TeraboxDownloader.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot.Polling;

namespace Devil7Softwares.TeraboxDownloader;

public class Program
{
    public static async Task Main()
    {
        Configuration configuration = new Configuration();

        ServiceProvider services = new ServiceCollection()
         .AddSingleton<IConfiguration>(configuration)
         .AddLogging((options) =>
         {
             options.AddConsole().AddSimpleConsole((options) =>
             {
                 options.IncludeScopes = true;
                 options.TimestampFormat = "[dd-MM-yyyy hh:mm:ss tt] ";
                 options.UseUtcTimestamp = true;
             });

             options.SetMinimumLevel(configuration.LogLevel);
         })
         .AddDbContext<DataContext>()
         .AddSingleton<IUpdateHandler, UpdateHandler>()
         .AddSingleton<IBot, Bot>()
         .AddSingleton<TeraboxDownloaderDotNetResolver>()
         .AddSingleton<UrlResolverFactory>(services => downloadMethod =>
         {
             return downloadMethod switch
             {
                 DownloadMethod.TeraboxDownloaderDotNet => services.GetRequiredService<TeraboxDownloaderDotNetResolver>(),
                 _ => throw new NotSupportedException($"Download method {downloadMethod} is not supported")
             };
         })
        .AddSingleton<IJobDownloaderFactory, JobDownloaderFactory>()
        .AddQuartz((options) =>
        {
            options.UseDefaultThreadPool((tp) =>
            {
                tp.MaxConcurrency = configuration.MaxConcurrentDownloads;
            });

            options.AddJob<DownloadJob>((job) =>
            {
                job.WithIdentity("DownloadJob").StoreDurably(true);
            });

            options.UseInMemoryStore();
        })
        .AddTransient<DownloadJob>()
        .BuildServiceProvider();

        Utils.Logging.LoggerFactory = services.GetRequiredService<ILoggerFactory>();

        ILogger logger = Utils.Logging.CreateLogger<Program>();

        logger.LogDebug("Initializing database");
        DataContext dataContext = services.GetRequiredService<DataContext>();
        dataContext.Database.EnsureCreated();

        logger.LogDebug("Initializing telegram bot");
        IBot bot = services.GetRequiredService<IBot>();
        bot.Start();

        logger.LogDebug("Initializing scheduler");
        ISchedulerFactory schedulerFactory = services.GetRequiredService<ISchedulerFactory>();

        logger.LogInformation("Scheduling existing jobs");

        DownloadJob.ScheduleExistingJobs(schedulerFactory, dataContext.Jobs.Where((job) => job.Status == JobStatus.Queued)).Wait();

        IScheduler scheduler = await schedulerFactory.GetScheduler();

        await scheduler.Start();

        logger.LogInformation("Application started");

        ManualResetEventSlim manualResetEvent = new ManualResetEventSlim(false);

        Console.CancelKeyPress += (sender, e) =>
        {
            logger.LogInformation("Stopping application");
            bot.Stop();
            scheduler.Shutdown(false);
            e.Cancel = true;
            manualResetEvent.Set();
            logger.LogInformation("Application stopped");
        };

        manualResetEvent.Wait();
    }
}