using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using WebApplication1;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWPF", policy =>
    {
        policy.WithOrigins("http://localhost", "https://localhost") // Разрешаем запросы с localhost
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql
    (
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    )
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<EventService>();
//builder.Services.AddSignalR();

// Регистрация EventCleanupService как scoped
builder.Services.AddScoped<EventCleanupService>();

// Настройка периодической задачи
builder.Services.AddHostedService<EventCleanupHostedService>();

var app = builder.Build();

// Инициализация тестовых данных при старте приложения (асинхронно)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var eventService = services.GetRequiredService<EventService>();
        await eventService.InitializeWithSampleDataAsync(); // Асинхронный вызов
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger?.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Включение OpenAPI в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowWPF"); // Применяем CORS
app.UseHttpsRedirection();

// Централизованная обработка ошибок (middleware)
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Произошла внутренняя ошибка сервера");
    });
});

// Маршруты API
app.MapGet("/api/events", async (EventService eventService) =>
    await Task.FromResult(Results.Ok(await eventService.GetAllAsync())));

app.MapGet("/api/events/upcoming", async (EventService eventService) =>
{
    var upcomingEvents = await eventService.GetUpcomingEventsAsync(24); // События в ближайшие 24 часа
    return Results.Ok(upcomingEvents);
});


app.MapGet("/api/events/{id}", async (int id, EventService eventService) =>
{
    var eventItem = await eventService.GetByIdAsync(id);
    return eventItem == null
        ? Results.NotFound($"Событие с ID {id} не найдено")
        : Results.Ok(eventItem);
});

app.MapGet("/api/events/search", async (
    EventService eventService,
    [FromQuery] string searchTerm = "",
    [FromQuery] DateTime? dateFrom = null,
    [FromQuery] DateTime? dateTo = null) =>
{
    var events = await eventService.SearchEventsAsync(searchTerm, dateFrom, dateTo);
    return Results.Ok(events);
});

app.MapPost("/api/events", async (Event eventItem, EventService eventService) =>
{
    if (string.IsNullOrWhiteSpace(eventItem.Title))
        return Results.BadRequest("Название события обязательно для заполнения");

    await eventService.CreateAsync(eventItem); // Асинхронный вызов
    return Results.Created($"/api/events/{eventItem.Id}", eventItem);
});

app.MapPut("/api/events/{id}", async (int id, Event eventItem, EventService eventService) =>
{
    if (id != eventItem.Id)
        return Results.BadRequest("ID в URL и в теле запроса не совпадают");

    if (await eventService.UpdateAsync(id, eventItem))
        return Results.Ok("Событие успешно обновлено");
    else
        return Results.NotFound($"Событие с ID {id} не найдено");
});

app.MapDelete("/api/events/{id}", async (int id, EventService eventService) =>
{
    if (await eventService.DeleteAsync(id))
        return Results.Ok("Событие успешно удалено");
    else
        return Results.NotFound($"Событие с ID {id} не найдено");
});

app.Run();