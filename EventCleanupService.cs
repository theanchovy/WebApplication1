using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;


namespace WebApplication1;

public class EventCleanupService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EventCleanupService> _logger;

    public EventCleanupService(AppDbContext context, ILogger<EventCleanupService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CleanupEventsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var cutoff = now.AddSeconds(-5); // для теста; события старше вчерашнего дня: var cutoff = now.AddDays(-1);

        // 1. Обновляем ежегодные события
        var eventsToUpdate = await _context.Events
            .Where(e => e.Annual && e.EventDate < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var e in eventsToUpdate)
        {
            var nextYear = e.EventDate.Year + 1;
            var isLeapNext = DateTime.IsLeapYear(nextYear);

            if (e.EventDate.Month == 2 && e.EventDate.Day == 29)
            {
                // Стратегия: если следующий год НЕ високосный — ставим 28 февраля
                if (!isLeapNext)
                {
                    e.EventDate = new DateTime(
                        nextYear,
                        2,
                        28,
                        e.EventDate.Hour,
                        e.EventDate.Minute,
                        e.EventDate.Second,
                        e.EventDate.Millisecond,
                        e.EventDate.Kind
                    );

                    _logger.LogInformation(
                        "Ежегодное событие '{Title}' (ID {Id}) было 29.02.{Year}, " +
                        "следующий год не високосный → перенесено на 28.02.{NextYear}",
                        e.Title, e.Id, e.EventDate.Year, nextYear
                    );
                }
                else
                {
                    // Следующий год високосный: оставляем 29 февраля через AddYears
                    e.EventDate = e.EventDate.AddYears(1);
                    _logger.LogDebug(
                        "Ежегодное событие '{Title}' (ID {Id}) осталось 29.02.{NextYear} (високосный год)",
                        e.Title, e.Id, nextYear
                    );
                }
            }
            else
            {
                // Обычная дата: просто добавляем год
                e.EventDate = e.EventDate.AddYears(1);
                _logger.LogDebug(
                    "Ежегодное событие '{Title}' (ID {Id}) перенесено на {NewDate}",
                    e.Title, e.Id, e.EventDate
                );
            }
        }

        // 2. Удаляем одноразовые (не ежегодные) события, которые уже прошли
        var eventsToDelete = await _context.Events
            .Where(e => !e.Annual && e.EventDate < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var e in eventsToDelete)
        {
            _context.Events.Remove(e);
            _logger.LogInformation(
                "Удалено одноразовое событие '{Title}' (ID {Id}, дата {Date})",
                e.Title, e.Id, e.EventDate
            );
        }

        if (eventsToDelete.Any() || eventsToUpdate.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Очистка завершена: обновлено {UpdatedCount} ежегодных, удалено {DeletedCount} одноразовых событий",
                eventsToUpdate.Count,
                eventsToDelete.Count
            );
        }
        else
        {
            _logger.LogDebug("Очистка: изменений не требуется");
        }
    }
}