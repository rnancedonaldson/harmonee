

var builder = DistributedApplication.CreateBuilder(args);

// var sql = builder
//     .AddSqlServer(Constants.Infrastructure.Database.ServerName)
//     .WithLifetime(ContainerLifetime.Persistent)
//     .WithContainerName(Constants.Infrastructure.Database.ContainerName);
// var cache = builder.AddRedis(Constants.Infrastructure.Cache.ContainerName);

// var authDb = sql.AddDatabase(Constants.Services.Auth.DatabaseName);
// var authService = builder
//     .AddProject<Projects.Harmonee_AuthService>(Constants.Services.Auth.ContainerName)
//     .WithReference(authDb)
//     .WaitFor(authDb);

// var familyDb = sql.AddDatabase(Constants.Services.Family.DatabaseName);
// var familyService = builder
//     .AddProject<Projects.Harmonee_FamilyService>(Constants.Services.Family.ContainerName)
//     .WithReference(familyDb)
//     .WaitFor(familyDb);

// var kitchenDb = sql.AddDatabase(Constants.Services.Kitchen.DatabaseName);
// var kitchenService = builder
//     .AddProject<Projects.Harmonee_KitchenService>(Constants.Services.Kitchen.ContainerName)
//     .WithReference(kitchenDb)
//     .WaitFor(kitchenDb);

// var scheduleDb = sql.AddDatabase(Constants.Services.Schedule.DatabaseName);
// var scheduleService = builder
//     .AddProject<Projects.Harmonee_ScheduleService>(Constants.Services.Schedule.ContainerName)
//     .WithReference(scheduleDb)
//     .WaitFor(scheduleDb);

// var webUI = builder.AddProject<Projects.Harmonee_Presentation>(Constants.Services.Presentation.ContainerName)
//     .WithReference(authService).WaitFor(authService)
//     .WithReference(familyService).WaitFor(familyService)
//     .WithReference(kitchenService).WaitFor(kitchenService)
//     .WithReference(scheduleService).WaitFor(scheduleService);

// var migrationService = builder.AddProject<Projects.Harmonee_MigrationService>(Constants.Services.Migration.ContainerName)
//     .WithReference(authDb).WaitFor(authDb)
//     .WithReference(familyDb).WaitFor(familyDb)
//     .WithReference(kitchenDb).WaitFor(kitchenDb)
//     .WithReference(scheduleDb).WaitFor(scheduleDb);

builder.Build().Run(); 