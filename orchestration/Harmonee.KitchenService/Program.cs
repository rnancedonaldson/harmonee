using Harmonee.KitchenService.Data;
using Harmonee.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.AddSqlServerDbContext<KitchenContext>(Constants.Services.Kitchen.DatabaseName);
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


$folderStructure = @{
    'src' = @{
        'Domain' = 'classlib'
        'Application' = 'classlib'
        'Infrastructure' = 'classlib'
        'Presentation' = 'classlib'
        'ServiceDefaults' = 'aspire-servicedefaults'
    }
    'tests' = @{
        'Domain.Tests' = 'xunit'
        'Application.Tests' = 'xunit'
        'Presentation.Tests' = 'xunit'
    }
    'docs' = @{}
    'orchestration' = @{
        'AppHost' = 'aspire-apphost'
    }
}