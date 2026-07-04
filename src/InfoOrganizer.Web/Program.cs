using System.Globalization;
using System.Diagnostics;
using System.Text;
using InfoOrganizer.Application;
using InfoOrganizer.Data;
using InfoOrganizer.Web.Components;
using InfoOrganizer.Web.State;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
ApplyEnvironmentUrlsOverride(builder, args);
var openBrowser = builder.Configuration.GetValue("Launcher:OpenBrowser", !builder.Environment.IsDevelopment());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? $"Data Source={AppPaths.GetDefaultDatabasePath()}";
builder.Services.AddInfoOrganizerData(connectionString);
builder.Services.AddInfoOrganizerApplication(builder.Configuration);

// Holds the in-flight import between the Upload and Review screens (per Blazor circuit).
builder.Services.AddScoped<ImportSession>();

var app = builder.Build();

// Apply migrations on startup so the SQLite file is ready out of the box.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (openBrowser)
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(app.Urls.FirstOrDefault()));

// Inventory export as CSV (no extra dependency).
app.MapGet("/export/inventory.csv", async (TrackingService tracking) =>
{
    var levels = await tracking.GetStockLevelsAsync();
    var sb = new StringBuilder();
    sb.AppendLine("Product,Code,Category,Unit,OnHand,ReorderAt,UnitCost,Value,Status");
    foreach (var l in levels)
        sb.AppendLine(string.Join(',',
            Csv(l.Name), Csv(l.Sku), Csv(l.Category), Csv(l.Unit),
            CsvNumber(l.OnHand), CsvNumber(l.ReorderThreshold), CsvNumber(l.UnitCost), CsvNumber(l.Value),
            l.IsLow ? "Low" : "OK"));
    return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "inventory.csv");
});

// Convenience for local demos: seed sample data.
if (app.Environment.IsDevelopment())
    app.MapPost("/dev/seed", async (SeedService seed) => Results.Ok(new { seeded = await seed.SeedSampleAsync() }));

app.Run();

static void OpenBrowser(string? address)
{
    if (string.IsNullOrWhiteSpace(address))
        return;

    try
    {
        Process.Start(new ProcessStartInfo(NormalizeListeningAddress(address))
        {
            UseShellExecute = true
        });
    }
    catch
    {
        // Browser launch is a convenience; the web server should keep running if it fails.
    }
}

static string NormalizeListeningAddress(string address)
{
    if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        return address;

    if (uri.Host is "0.0.0.0" or "::" or "+" or "*")
    {
        var builder = new UriBuilder(uri) { Host = "localhost" };
        return builder.Uri.ToString();
    }

    return address;
}

static void ApplyEnvironmentUrlsOverride(WebApplicationBuilder builder, string[] args)
{
    if (HasUrlsArgument(args))
        return;

    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (string.IsNullOrWhiteSpace(urls))
        urls = Environment.GetEnvironmentVariable("DOTNET_URLS");

    if (string.IsNullOrWhiteSpace(urls))
        return;

    var values = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (values.Length > 0)
        builder.WebHost.UseUrls(values);
}

static bool HasUrlsArgument(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals("--urls", StringComparison.OrdinalIgnoreCase)
            || args[i].Equals("/urls", StringComparison.OrdinalIgnoreCase)
            || args[i].StartsWith("--urls=", StringComparison.OrdinalIgnoreCase)
            || args[i].StartsWith("/urls=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static string Csv(string? value)
{
    value ??= "";
    return value.Contains(',') || value.Contains('"') || value.Contains('\n')
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}

static string CsvNumber(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
