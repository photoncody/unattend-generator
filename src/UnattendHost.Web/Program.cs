using Microsoft.AspNetCore.DataProtection;
using Schneegans.Unattend;
using UnattendHost.Core;
using UnattendHost.Core.Presets;
using UnattendHost.Web;

if (args.Contains("--healthcheck"))
{
    return await HealthCheck.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddRazorPages();

// Keys live in memory only: the container has a read-only file system, and the keys only
// protect antiforgery tokens. A restart invalidates forms that are open in a browser, nothing else.
// (appsettings.json silences the resulting "keys will not be persisted" warnings.)
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

builder.Services.AddSingleton<UnattendGenerator>();
builder.Services.AddSingleton<UpstreamCatalog>();
builder.Services.AddSingleton(sp => new GenerationService(sp.GetRequiredService<UnattendGenerator>()));
builder.Services.AddSingleton(new PresetStore(Path.GetFullPath(
    builder.Configuration["UnattendHost:PresetDirectory"] ?? "presets",
    builder.Environment.ContentRootPath)));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    // No inline scripts or styles, and nothing loaded from other origins.
    headers.ContentSecurityPolicy = "default-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseStaticFiles();
app.MapRazorPages();

app.MapGet("/healthz", () => Results.Json(new
{
    status = "ok",
    upstreamCommit = BuildInfo.UpstreamCommit,
    appVersion = BuildInfo.AppVersion,
}));

app.Run();
return 0;

public partial class Program;
