using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication1
{
    public class EventCleanupService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EventCleanupService> _logger;

        public EventCleanupService(AppDbContext context, ILogger<EventCleanupService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CleanupEventsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Запуск очистки событий...");

                var now = DateTime.Now;
                var eventsToDelete = await _context.Events
                    .Where(e => !e.Annual && e.EventDate < now.AddSeconds(-5)) // .Where(e => !e.Annual && e.EventDate < now.AddDays(-1))
                    .ToListAsync(cancellationToken);

                foreach (var eventItem in eventsToDelete)
                {
                    _logger.LogInformation($"Удаляю событие '{eventItem.Title}' с ID {eventItem.Id}, так как оно не ежегодное и наступило более суток назад.");
                    _context.Events.Remove(eventItem);
                }

                var eventsToUpdate = await _context.Events
                    .Where(e => e.Annual && e.EventDate < now.AddSeconds(-5)) // .Where(e => e.Annual && e.EventDate < now.AddDays(-1))
                    .ToListAsync(cancellationToken);

                foreach (var eventItem in eventsToUpdate)
                {
                    eventItem.EventDate = eventItem.EventDate.AddYears(1);
                    _logger.LogInformation($"Обновляю событие '{eventItem.Title}' с ID {eventItem.Id} — добавляю один год к дате.");
                }

                if (eventsToDelete.Any() || eventsToUpdate.Any())
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Нет событий для очистки или обновления.");
                }

                _logger.LogInformation("Очистка событий завершена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке событий");
                throw;
            }
        }
    }
}