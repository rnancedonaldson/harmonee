using Harmonee.Calendar;
using Harmonee.Families;
using Harmonee.Kitchen;
using Harmonee.MigrationService;
using Harmonee.Notes;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddFamiliesModule()
    .AddCalendarModule()
    .AddKitchenModule()
    .AddNotesModule();

builder.Services.AddHostedService<MigrationWorker>();

await builder.Build().RunAsync();
