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

// In Azure: database users for the app identities (Database:Users), created after the schema is up to date.
builder.Services.Configure<DatabaseUsersOptions>(builder.Configuration.GetSection(DatabaseUsersOptions.SectionName));
builder.Services.AddTransient<DatabaseUserProvisioner>();

builder.Services.AddHostedService<MigrationWorker>();

await builder.Build().RunAsync();
