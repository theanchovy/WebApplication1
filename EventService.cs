using Microsoft.EntityFrameworkCore;

namespace WebApplication1
{
    public class EventService
    {
        private readonly AppDbContext _context;

        public EventService(AppDbContext context)
        {
            _context = context;
        }

        public async Task InitializeWithSampleDataAsync()
        {
            if (await _context.Events.AnyAsync())
                return;

            var today = DateTime.Today;

            _context.Events.AddRange(new List<Event>
        {
            new Event
            {
                Title = "Новый год",
                Description = "Празднуем начало нового года!",
                EventDate = new DateTime(today.Year + 1, 1, 1),
                Annual = true
            },
            new Event
            {
                Title = "День программиста",
                Description = "Профессиональный праздник",
                EventDate = new DateTime(today.Year, 9, 13),
                Annual = false
            }
        });

            await _context.SaveChangesAsync();
        }

        public async Task<List<Event>> GetAllAsync() => await _context.Events.ToListAsync();

        public async Task<Event?> GetByIdAsync(int id) => await _context.Events.FindAsync(id);

        public async Task<List<Event>> GetUpcomingEventsAsync(int hours = 24)
        {
            var now = DateTime.Now; // Текущее время с часами и минутами
            return await _context.Events
                .Where(e => e.EventDate >= now && e.EventDate <= now.AddHours(hours))
                .OrderBy(e => e.EventDate)
                .ToListAsync();
        }

        public async Task CreateAsync(Event eventItem)
        {
            if (eventItem == null)
                throw new ArgumentNullException(nameof(eventItem));

            _context.Events.Add(eventItem);
            await _context.SaveChangesAsync();

        }

        public async Task<bool> UpdateAsync(int id, Event eventItem)
        {
            if (eventItem == null)
                throw new ArgumentNullException(nameof(eventItem));
            if (id <= 0)
                throw new ArgumentException("ID должен быть положительным", nameof(id));

            var existingEvent = await _context.Events.FindAsync(id);
            if (existingEvent == null)
                return false;

            existingEvent.Title = eventItem.Title;
            existingEvent.Description = eventItem.Description;
            existingEvent.EventDate = eventItem.EventDate;
            existingEvent.Annual = eventItem.Annual;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID должен быть положительным", nameof(id));

            var eventToDelete = await _context.Events.FindAsync(id);
            if (eventToDelete == null)
                return false;

            _context.Events.Remove(eventToDelete);
            await _context.SaveChangesAsync();
            return true;
        }


        public async Task<List<Event>> SearchEventsAsync(string searchTerm, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var query = _context.Events.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(e =>
                    e.Title.Contains(searchTerm) ||
                    (e.Description != null && e.Description.Contains(searchTerm)));
            }

            if (dateFrom.HasValue)
            {
                var fromDate = dateFrom.Value.Date;
                query = query.Where(e => e.EventDate >= fromDate);
            }

            if (dateTo.HasValue)
            {
                // Используем начало следующего дня для точного включения всех событий текущего дня
                var toDate = dateTo.Value.Date.AddDays(1);
                query = query.Where(e => e.EventDate < toDate);
            }

            return await query.OrderBy(e => e.EventDate).ToListAsync();
        }
    }
}