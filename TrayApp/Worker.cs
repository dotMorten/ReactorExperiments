namespace ReactorTrayWorker;

public class Worker(IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = Task.Run(() =>
        {
            TrayService.InitializeWinUIWindow(hostApplicationLifetime.StopApplication);
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }
}
