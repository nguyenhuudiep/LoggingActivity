using LoggingActivity.Web.Data;
using LoggingActivity.Web.Options;
using LoggingActivity.Web.Repositories;
using LoggingActivity.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

builder.Services
    .AddOptions<MongoDbSettings>()
    .Bind(builder.Configuration.GetSection(MongoDbSettings.SectionName))
    .PostConfigure(settings =>
    {
        settings.ConnectionString = ResolveSetting(
            settings.ConnectionString,
            builder.Configuration.GetConnectionString("MongoDb"),
            builder.Configuration["MONGODB_URI"]);

        settings.DatabaseName = ResolveSetting(
            settings.DatabaseName,
            builder.Configuration["MONGODB_DATABASE"]);
    })
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConnectionString)
        && !settings.ConnectionString.Contains("<set-via-user-secrets-or-env>", StringComparison.OrdinalIgnoreCase),
        "MongoDb:ConnectionString must be configured via user-secrets or environment variables.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.DatabaseName)
        || !string.IsNullOrWhiteSpace(MongoDB.Driver.MongoUrl.Create(settings.ConnectionString).DatabaseName),
        "MongoDb:DatabaseName must be configured directly or included in the connection string.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SeedAdminOptions>()
    .Bind(builder.Configuration.GetSection(SeedAdminOptions.SectionName))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.UserName)
        && !settings.UserName.Contains("<set-via-user-secrets-or-env>", StringComparison.OrdinalIgnoreCase),
        "SeedAdmin:UserName must be configured via user-secrets or environment variables.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Email)
        && !settings.Email.Contains("<set-via-user-secrets-or-env>", StringComparison.OrdinalIgnoreCase),
        "SeedAdmin:Email must be configured via user-secrets or environment variables.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Password)
        && !settings.Password.Contains("<set-via-user-secrets-or-env>", StringComparison.OrdinalIgnoreCase),
        "SeedAdmin:Password must be configured via user-secrets or environment variables.")
    .ValidateOnStart();

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
builder.Services.AddScoped<IActivityLogIngestQueueRepository, ActivityLogIngestQueueRepository>();
builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
builder.Services.AddScoped<IAlertHistoryRepository, AlertHistoryRepository>();
builder.Services.AddScoped<ILogActionDefinitionRepository, LogActionDefinitionRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PartnerService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddScoped<ActivityLogIngestQueueService>();
builder.Services.AddScoped<AlertRuleService>();
builder.Services.AddScoped<AlertHistoryService>();
builder.Services.AddScoped<LogActionDefinitionService>();
builder.Services.AddHostedService<SeedAdminHostedService>();
builder.Services.AddHostedService<ActivityLogIngestProcessorHostedService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

var supportedCulture = new CultureInfo("vi-VN");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(supportedCulture),
    SupportedCultures = new[] { supportedCulture },
    SupportedUICultures = new[] { supportedCulture }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string ResolveSetting(params string?[] candidates)
{
    foreach (var candidate in candidates)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            continue;
        }

        if (candidate.Contains("<set-via-user-secrets-or-env>", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        return candidate.Trim();
    }

    return string.Empty;
}
