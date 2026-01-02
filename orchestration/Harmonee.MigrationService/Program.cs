using Harmonee.AuthService.Data;
using Harmonee.FamilyService.Data;
using Harmonee.KitchenService.Data;
using Harmonee.MigrationService;
using Harmonee.Resources;
using Harmonee.ScheduleService.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.AddSqlServerDbContext<HarmoneeAuthContext>(Constants.Services.Auth.DatabaseName);
builder.AddSqlServerDbContext<FamilyContext>(Constants.Services.Family.DatabaseName);
builder.AddSqlServerDbContext<KitchenContext>(Constants.Services.Kitchen.DatabaseName);
builder.AddSqlServerDbContext<ScheduleContext>(Constants.Services.Schedule.DatabaseName);

var host = builder.Build();
host.Run();
