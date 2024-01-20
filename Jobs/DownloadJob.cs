using Devil7Softwares.TeraboxDownloader.Database;
using Devil7Softwares.TeraboxDownloader.Database.Models;
using Devil7Softwares.TeraboxDownloader.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Devil7Softwares.TeraboxDownloader.Jobs;

internal class DownloadJob : IJob
{
    private readonly DataContext _dataContext;
    private readonly ILogger<DownloadJob> _logger;

    public DownloadJob(ILogger<DownloadJob> logger, DataContext dataContext)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        Guid jobId = context.Trigger.JobDataMap.GetGuid("Job");

        _logger.LogInformation($"Executing job {jobId}");

        JobEntity? job = await _dataContext.Jobs
            .Include((job) => job.Chat)
            .ThenInclude((chat) => chat!.Config)
            .FirstOrDefaultAsync((job) => job.Id == jobId);

        if (job is null)
        {
            _logger.LogError("Job with id {JobId} not found", jobId);
            return;
        }

        try
        {
            // TODO: Download files

            job.Status = JobStatus.Completed;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;

            _logger.LogError(ex, "Failed to download files");
        }
        finally
        {
            await _dataContext.SaveChangesAsync();
        }
    }

    public static async Task ScheduleJob(ISchedulerFactory schedulerFactory, JobEntity job)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler();

        await ScheduleJob(scheduler, job);
    }

    public static async Task ScheduleJob(IScheduler scheduler, JobEntity job)
    {
        ILogger logger = Utils.Logging.CreateLogger<DownloadJob>();

        JobDataMap jobDataMap = new JobDataMap
        {
            { "Job", job.Id }
        };

        await scheduler.TriggerJob(new JobKey("DownloadJob"), jobDataMap);
        logger.LogInformation($"Scheduled job {job.Id}");
    }

    public static async Task ScheduleExistingJobs(ISchedulerFactory schedulerFactory, IEnumerable<JobEntity> jobs)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler();

        foreach (JobEntity job in jobs)
        {
            await ScheduleJob(scheduler, job);
        }
    }
}
