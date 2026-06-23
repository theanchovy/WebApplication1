using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication1
{
    public class EventCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EventCleanupHostedService> _logger;

        public EventCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<EventCleanupHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Сервис очистки событий запущен.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var cleanupService = scope.ServiceProvider.GetRequiredService<EventCleanupService>();
                    await cleanupService.CleanupEventsAsync(stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в процессе очистки событий");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("Сервис очистки событий остановлен.");
        }
    }
}