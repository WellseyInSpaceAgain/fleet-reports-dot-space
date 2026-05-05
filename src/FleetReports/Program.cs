using FleetReports.Components;
using FleetReports.Models;
using FleetReports.Services;
using LiteDB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "fleet-reports.db");
builder.Services.AddSingleton<LiteDatabase>(_ =>
{
    var db = new LiteDatabase(dbPath);
    db.GetCollection<KillmailDocument>("killmails").EnsureIndex(x => x.KillmailTime);
    db.GetCollection<ReportDocument>("reports").EnsureIndex(x => x.CreatedAt);
    return db;
});

builder.Services.AddHttpClient("esi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://esi.evetech.net/latest/");
    client.DefaultRequestHeaders.Add("User-Agent", config["Esi:UserAgent"]);
    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
});

builder.Services.AddHttpClient("zkb", client =>
{
    client.BaseAddress = new Uri("https://zkillboard.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "fleet-reports/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient("r2z2", client =>
{
    client.BaseAddress = new Uri("https://r2z2.zkillboard.com/ephemeral/");
    client.DefaultRequestHeaders.Add("User-Agent", "fleet-reports/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddScoped<IEsiService, EsiService>();
builder.Services.AddSingleton<ISystemNameCacheService, SystemNameCacheService>();
builder.Services.AddSingleton<IKillmailCacheService, KillmailCacheService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IZkillCharacterFetcher, ZkillCharacterFetcher>();
builder.Services.AddScoped<IR2Z2HistoricalFetcher, R2Z2HistoricalFetcher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
