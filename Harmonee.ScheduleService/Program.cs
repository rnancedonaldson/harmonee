using Harmonee.Resources;
using Harmonee.ScheduleService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddSqlServerDbContext<ScheduleContext>(Constants.Services.Schedule.DatabaseName);
builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();