using LoggingActivity.Web.Data;
using LoggingActivity.Web.Options;
using LoggingActivity.Web.Repositories;
using LoggingActivity.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var deployedBaseDirectory = AppContext.BaseDirectory;
var deployedWebRoot = Path.Combine(deployedBaseDirectory, "wwwroot");
if (!builder.Environment.IsDevelopment() && Directory.Exists(deployedWebRoot))
{
    builder.WebHost.UseContentRoot(deployedBaseDirectory);
    builder.WebHost.UseWebRoot(deployedWebRoot);
}

var configuredDpPath = builder.Configuration["APP_DATA_PROTECTION_KEYS_PATH"];
var dataProtectionKeysPath = string.IsNullOrWhiteSpace(configuredDpPath)
    ? Path.Combine(Path.GetTempPath(), "loggingactivity-dpkeys")
    : configuredDpPath.Trim();
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .SetApplicationName("LoggingActivity.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

var enableHttpsRedirection = !string.Equals(
    builder.Configuration["APP_ENABLE_HTTPS_REDIRECTION"],
    "false",
    StringComparison.OrdinalIgnoreCase);

builder.Configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

var useLocalSettings = string.Equals(
    builder.Configuration["APP_USE_LOCAL_SETTINGS"],
    "true",
    StringComparison.OrdinalIgnoreCase)
    || builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Local");

if (useLocalSettings)
{
    builder.Configuration
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);
}

// Re-apply user-secrets so local json defaults do not override developer-specific secrets.
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Local"))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

var disableMongoBackgroundWorkers =
    bool.TryParse(builder.Configuration["AppRuntime:DisableMongoBackgroundWorkers"], out var disableFromConfig)
        ? disableFromConfig
        : builder.Environment.IsDevelopment()
            || builder.Environment.IsEnvironment("Local")
            || string.Equals(
                builder.Configuration["APP_DISABLE_MONGO_BACKGROUND_SERVICES"],
                "true",
                StringComparison.OrdinalIgnoreCase);

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
    .PostConfigure(settings =>
    {
        settings.UserName = ResolveSetting(settings.UserName);
        settings.Email = ResolveSetting(settings.Email);
        settings.Password = ResolveSetting(settings.Password);

        if (!settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.UserName)
            || string.IsNullOrWhiteSpace(settings.Email)
            || string.IsNullOrWhiteSpace(settings.Password))
        {
            settings.Enabled = false;
        }
    })
    .Validate(settings => !settings.Enabled
        || (!string.IsNullOrWhiteSpace(settings.UserName)
            && !string.IsNullOrWhiteSpace(settings.Email)
            && !string.IsNullOrWhiteSpace(settings.Password)),
        "SeedAdmin requires UserName, Email, and Password when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<ThresholdNotificationOptions>()
    .Bind(builder.Configuration.GetSection(ThresholdNotificationOptions.SectionName));
builder.Services
    .AddOptions<CitizenIdHybridModelOptions>()
    .Bind(builder.Configuration.GetSection(CitizenIdHybridModelOptions.SectionName))
    .PostConfigure(settings =>
    {
        settings.Endpoint = ResolveSetting(
            settings.Endpoint,
            builder.Configuration["CITIZEN_ID_HYBRID_MODEL_ENDPOINT"]);
        settings.ApiKey = ResolveSetting(
            settings.ApiKey,
            builder.Configuration["CITIZEN_ID_HYBRID_MODEL_API_KEY"]);

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            settings.Enabled = false;
        }

        settings.TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds, 1, 30);
        settings.MinConfidence = Math.Clamp(settings.MinConfidence, 0.5, 0.99);
    });
builder.Services
    .AddOptions<CitizenIdOcrOptions>()
    .Bind(builder.Configuration.GetSection(CitizenIdOcrOptions.SectionName))
    .PostConfigure(settings =>
    {
        settings.Endpoint = ResolveSetting(
            settings.Endpoint,
            builder.Configuration["CITIZEN_ID_OCR_ENDPOINT"]);
        settings.ApiKey = ResolveSetting(
            settings.ApiKey,
            builder.Configuration["CITIZEN_ID_OCR_API_KEY"]);

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            settings.Enabled = false;
        }

        settings.TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds, 1, 30);
    });
builder.Services
    .AddOptions<VehicleRegistrationOcrOptions>()
    .Bind(builder.Configuration.GetSection(VehicleRegistrationOcrOptions.SectionName))
    .PostConfigure(settings =>
    {
        var openAiApiKeyFromEnv = builder.Configuration["VEHICLE_REG_OCR_OPENAI_API_KEY"];
        var openAiEndpointFromEnv = builder.Configuration["VEHICLE_REG_OCR_OPENAI_ENDPOINT"];
        var openAiModelFromEnv = builder.Configuration["VEHICLE_REG_OCR_OPENAI_MODEL"];

        settings.Provider = ResolveSetting(
            settings.Provider,
            builder.Configuration["VEHICLE_REG_OCR_PROVIDER"]);
        settings.OpenAiEndpoint = ResolveSetting(
            settings.OpenAiEndpoint,
            openAiEndpointFromEnv);
        settings.OpenAiApiKey = ResolveSetting(
            settings.OpenAiApiKey,
            openAiApiKeyFromEnv,
            settings.ApiKey,
            builder.Configuration["VEHICLE_REG_OCR_API_KEY"]);
        settings.OpenAiModel = ResolveSetting(
            settings.OpenAiModel,
            openAiModelFromEnv);
        settings.CustomEndpoint = ResolveSetting(
            settings.CustomEndpoint,
            builder.Configuration["VEHICLE_REG_OCR_CUSTOM_ENDPOINT"]);
        settings.CustomApiKey = ResolveSetting(
            settings.CustomApiKey,
            builder.Configuration["VEHICLE_REG_OCR_CUSTOM_API_KEY"]);
        settings.CustomApiKeyHeaderName = ResolveSetting(
            settings.CustomApiKeyHeaderName,
            builder.Configuration["VEHICLE_REG_OCR_CUSTOM_API_KEY_HEADER"]);
        settings.Endpoint = ResolveSetting(
            settings.Endpoint,
            builder.Configuration["VEHICLE_REG_OCR_ENDPOINT"]);
        settings.ApiKey = ResolveSetting(
            settings.ApiKey,
            builder.Configuration["VEHICLE_REG_OCR_API_KEY"]);
        settings.Model = ResolveSetting(
            settings.Model,
            builder.Configuration["VEHICLE_REG_OCR_MODEL"]);

        settings.Provider = string.IsNullOrWhiteSpace(settings.Provider)
            ? "openai"
            : settings.Provider.Trim().ToLowerInvariant();
        if (settings.Provider is not "openai" and not "custom")
        {
            settings.Provider = "openai";
        }

        // Minimal production setup: when only VEHICLE_REG_OCR_OPENAI_API_KEY is provided,
        // auto-enable OCR and use OpenAI defaults without requiring extra config.
        if (!string.IsNullOrWhiteSpace(openAiApiKeyFromEnv))
        {
            settings.Enabled = true;
            settings.Provider = "openai";
        }

        if (string.Equals(settings.Provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            settings.OpenAiEndpoint = ResolveSetting(settings.OpenAiEndpoint, "https://api.openai.com/v1/responses");
            settings.OpenAiModel = ResolveSetting(settings.OpenAiModel, "gpt-4.1");

            // Keep backward-compatible fields synchronized for components that still read base properties.
            settings.Endpoint = ResolveSetting(settings.Endpoint, settings.OpenAiEndpoint);
            settings.Model = ResolveSetting(settings.Model, settings.OpenAiModel);
            settings.ApiKey = ResolveSetting(settings.ApiKey, settings.OpenAiApiKey);
        }

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            settings.Endpoint = settings.Provider == "openai"
                ? "https://api.openai.com/v1/responses"
                : string.Empty;
        }

        settings.TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds, 3, 45);
        settings.MinConfidence = Math.Clamp(settings.MinConfidence, 0.5, 0.99);
    });

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPermissionGroupRepository, PermissionGroupRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
builder.Services.AddScoped<IActivityLogIngestQueueRepository, ActivityLogIngestQueueRepository>();
builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
builder.Services.AddScoped<IAlertHistoryRepository, AlertHistoryRepository>();
builder.Services.AddScoped<ILogActionDefinitionRepository, LogActionDefinitionRepository>();
builder.Services.AddScoped<ISystemAccessLogRepository, SystemAccessLogRepository>();
builder.Services.AddScoped<IUserActiveSessionRepository, UserActiveSessionRepository>();
builder.Services.AddScoped<IPartnerUserActionLimitRuleRepository, PartnerUserActionLimitRuleRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PermissionGroupService>();
builder.Services.AddScoped<PartnerService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SystemAccessAuditService>();
builder.Services.AddScoped<SingleSessionCookieEvents>();
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddScoped<ActivityLogIngestQueueService>();
builder.Services.AddScoped<AlertRuleService>();
builder.Services.AddScoped<AlertHistoryService>();
builder.Services.AddScoped<LogActionDefinitionService>();
builder.Services.AddScoped<PartnerUserActionLimitService>();
builder.Services.AddScoped<CitizenIdDetectionService>();
builder.Services.AddHttpClient<VehicleRegistrationOcrService>();
builder.Services.AddHttpClient<CitizenIdHybridModelClient>();
builder.Services.AddHttpClient<CitizenIdOcrClient>();
builder.Services.AddHttpClient<ThresholdNotificationService>();
builder.Services.AddHostedService<ActivityLogInfrastructureHostedService>();
builder.Services.AddHostedService<SeedAdminHostedService>();
if (!disableMongoBackgroundWorkers)
{
    builder.Services.AddHostedService<ActivityLogIngestProcessorHostedService>();
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(SingleSessionCookieEvents);
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

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
    if (enableHttpsRedirection)
    {
        app.UseHttpsRedirection();
    }
}
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
