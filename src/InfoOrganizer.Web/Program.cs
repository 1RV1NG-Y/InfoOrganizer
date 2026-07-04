using System.Globalization;
using System.Diagnostics;
using System.Text;
using InfoOrganizer.Application;
using InfoOrganizer.Data;
using InfoOrganizer.Web.Components;
using InfoOrganizer.Web.State;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
ApplyEnvironmentUrlsOverride(builder, args);
var openBrowser = builder.Configuration.GetValue("Launcher:OpenBrowser", !builder.Environment.IsDevelopment());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Localization: the AppStrings marker type lives in the InfoOrganizer.Web.Resources namespace,
// which already resolves to the /Resources folder — no ResourcesPath prefix needed.
builder.Services.AddLocalization();

// The product targets small Mexican businesses: default to Spanish (es-MX), keep English available.
var supportedCultures = new[] { "es-MX", "es", "en-US", "en" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(SupportedCultures.Default)
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    // Cookie first (explicit user choice), then the browser's Accept-Language header.
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

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

// Set the request culture (from cookie, then Accept-Language, then es-MX) before rendering.
// The Blazor circuit's culture is fixed at connection start, so a language change needs a full reload.
app.UseRequestLocalization();

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

// Language switcher. A plain HTTP endpoint is required because the Blazor circuit's culture is
// fixed at connection start; setting the cookie here + redirecting forces a fresh circuit.
app.MapGet("/culture/set", (HttpContext context, string? culture, string? redirectUri) =>
{
    if (string.IsNullOrWhiteSpace(culture) || !SupportedCultures.IsSupported(culture))
        return Results.BadRequest("Unsupported culture.");

    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    var target = LocalRedirectTarget(redirectUri);
    return Results.LocalRedirect(target);
});

// Convenience for local demos: seed sample data.
if (app.Environment.IsDevelopment())
    app.MapPost("/dev/seed", async (SeedService seed) => Results.Ok(new { seeded = await seed.SeedSampleAsync() }));

app.Run();

static string LocalRedirectTarget(string? redirectUri)
{
    // Only allow local, root-relative redirects to avoid open-redirect issues.
    if (!string.IsNullOrWhiteSpace(redirectUri)
        && redirectUri.StartsWith('/')
        && !redirectUri.StartsWith("//")
        && !redirectUri.StartsWith("/\\"))
    {
        return redirectUri;
    }

    return "/";
}

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

/// <summary>The UI cultures the app supports, and the default (es-MX for small Mexican businesses).</summary>
static class SupportedCultures
{
    public const string Default = "es-MX";

    private static readonly HashSet<string> Names =
        new(new[] { "es-MX", "es", "en-US", "en" }, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string culture) => Names.Contains(culture);
}
