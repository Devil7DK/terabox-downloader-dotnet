using Devil7Softwares.TeraboxDownloader.Database;
using Devil7Softwares.TeraboxDownloader.Database.Models;
using Devil7Softwares.TeraboxDownloader.Enums;
using Devil7Softwares.TeraboxDownloader.Telegram;
using Devil7Softwares.TeraboxDownloader.Terabox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;

namespace Devil7Softwares.TeraboxDownloader.Jobs;

internal class DownloadJob : IJob
{
    private readonly IBot _bot;
    private readonly DataContext _dataContext;
    private readonly IJobDownloaderFactory _jobDownloaderFactory;
    private readonly ILogger<DownloadJob> _logger;

    public DownloadJob(IBot bot, ILogger<DownloadJob> logger, DataContext dataContext, IJobDownloaderFactory jobDownloaderFactory)
    {
        _bot = bot;
        _dataContext = dataContext;
        _jobDownloaderFactory = jobDownloaderFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        Guid jobId = context.Trigger.JobDataMap.GetGuid("Job");

        try
        {
            _logger.LogInformation($"Executing job {jobId}");

            JobEntity? job = await _dataContext.Jobs
                .Include((job) => job.Chat)
                .ThenInclude((chat) => chat!.Config)
                .FirstOrDefaultAsync((job) => job.Id == jobId);

            if (context.CancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Job {jobId} cancelled");
                try
                {
                    job!.Status = JobStatus.Cancelled;
                    await _dataContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update job status");
                }
                return;
            }

            if (job is null)
            {
                _logger.LogError("Job with id {JobId} not found", jobId);
                return;
            }

            try
            {
                JobDownloader downloader = await _jobDownloaderFactory.Create(jobId, context.CancellationToken);

                await downloader.DownloadAsync();
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;

                _logger.LogError(ex, "Failed to download files");

                await _bot.Client.SendTextMessageAsync(job.Chat!.Id, $"URL: {job.Url}\nStatus: Failed - {ex.Message}", cancellationToken: context.CancellationToken);
            }
            finally
            {
                await _dataContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute job {JobId}", jobId);
        }
    }

    public static async Task ScheduleJob(ISchedulerFactory schedulerFactory, Guid jobId)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler();

        await ScheduleJob(scheduler, jobId);
    }

    public static async Task ScheduleJob(IScheduler scheduler, Guid jobId)
    {
        ILogger logger = Utils.Logging.CreateLogger<DownloadJob>();

        JobDataMap jobDataMap = new JobDataMap
        {
            { "Job", jobId }
        };

        await scheduler.TriggerJob(new JobKey("DownloadJob"), jobDataMap);
        logger.LogInformation($"Scheduled job {jobId}");
    }

    public static async Task ScheduleExistingJobs(ISchedulerFactory schedulerFactory, IEnumerable<JobEntity> jobs)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler();

        foreach (JobEntity job in jobs)
        {
            await ScheduleJob(scheduler, job.Id);
        }
    }
}
