using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Netrom_Eco_Meal.Components;
using Netrom_Eco_Meal.Controllers;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.AI;
using Netrom_Eco_Meal.Services.Email;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Services.Payments;
using OllamaSharp;
using Serilog;

// Single-locale app: prices are always RON, so every ToString("C") call site (cart,
// package cards, orders...) gets that formatting for free instead of the server's OS culture.
var romanianCulture = new CultureInfo("ro-RO");
CultureInfo.DefaultThreadCurrentCulture = romanianCulture;
CultureInfo.DefaultThreadCurrentUICulture = romanianCulture;

// Catches anything that fails before the real, config-driven logger below is up.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Sinks/levels live under "Serilog" in appsettings.json — see that file for the default shape.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId());

// Left unset when Stripe:SecretKey isn't configured — StripeGateway.EnsureConfigured then turns
// any checkout attempt into a friendly "payments aren't configured yet" error instead of an SDK
// exception, same as SmtpEmailSender degrading when Email:Smtp:Host is missing.
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;

// Left unregistered when Ollama:BaseUrl isn't configured — PackageAiAssistant's IChatClient?
// constructor parameter then resolves to null and turns any AI-feature attempt into a friendly
// "aren't available yet" error, same convention as Stripe:SecretKey above. Runs against a free,
// self-hosted Ollama instance (docker-compose.test.yml) — no hosted/paid API involved.
var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"];
if (!string.IsNullOrWhiteSpace(ollamaBaseUrl))
{
    var ollamaModelId = builder.Configuration["Ollama:ModelId"] ?? "qwen2.5:7b";
    builder.Services.AddSingleton<IChatClient>(new OllamaApiClient(new Uri(ollamaBaseUrl), ollamaModelId));
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var connectionString = builder.Configuration.GetConnectionString("EcoMealContext");
// Lets NotificationRepository open short-lived contexts, so the polling bell doesn't race the
// page over the shared per-circuit EcoMealDbContext below (also sourced from this factory).
builder.Services.AddDbContextFactory<EcoMealDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<EcoMealDbContext>(sp => sp.GetRequiredService<IDbContextFactory<EcoMealDbContext>>().CreateDbContext());

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Off by default so a bare `dotnet run`/demo works without SMTP configured — see
    // Email:Smtp:* / SmtpEmailSender for what turning this on requires.
    options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", false);
    options.Password.RequiredLength = 8;
}).AddEntityFrameworkStores<EcoMealDbContext>().AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
});

builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IBusinessTypeRepository, BusinessTypeRepository>();
builder.Services.AddScoped<IBusinessTypeService, BusinessTypeService>();
builder.Services.AddScoped<IPackageRepository, PackageRepository>();
builder.Services.AddScoped<IPackageTypeRepository, PackageTypeRepository>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IPackageTypeService, PackageTypeService>();
builder.Services.AddScoped<IPackageTemplateRepository, PackageTemplateRepository>();
builder.Services.AddScoped<IPackageTemplateService, PackageTemplateService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IImpactService, ImpactService>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
builder.Services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
builder.Services.AddScoped<IWebPushGateway, WebPushGateway>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IPackageAiAssistant, PackageAiAssistant>();
builder.Services.AddScoped<ISearchIntentParser, SearchIntentParser>();
builder.Services.AddScoped<INearExpiryNudgeComposer, NearExpiryNudgeComposer>();
builder.Services.AddScoped<INearExpiryNudgeService, NearExpiryNudgeService>();
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<CurrentUserAccessor>();
// Singleton, not Scoped — see PackageStockBroadcaster's own comment for why one instance needs to
// be shared across every circuit instead of living per-circuit like CartService below.
builder.Services.AddSingleton<PackageStockBroadcaster>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ClientTimeZoneService>();
builder.Services.AddScoped<ManagedBusinessContext>();
builder.Services.AddScoped<NotificationPanelState>();
builder.Services.AddControllers();
builder.Services.AddScoped<BusinessController>();
builder.Services.AddScoped<BusinessTypeController>();
builder.Services.AddScoped<PackageController>();
builder.Services.AddScoped<PackageTypeController>();
builder.Services.AddScoped<PackageTemplateController>();
builder.Services.AddScoped<UserController>();
builder.Services.AddScoped<OrderController>();
builder.Services.AddScoped<PaymentController>();
builder.Services.AddScoped<ReviewController>();
builder.Services.AddScoped<NotificationController>();
builder.Services.AddScoped<FavoriteController>();
builder.Services.AddScoped<AuditLogController>();
builder.Services.AddScoped<ReportController>();
builder.Services.AddScoped<PushSubscriptionController>();
builder.Services.AddScoped<ImpactController>();
// Real HTTP endpoint for Login/Register/Logout (see AuthController), but also registered here so
// ConfirmEmail/ForgotPassword/ResetPassword can inject it in-process like every other controller.
builder.Services.AddScoped<AuthController>();
builder.Services.AddHostedService<OrderLifecycleSweepService>();
builder.Services.AddHostedService<PackageTemplateGenerationService>();
builder.Services.AddHostedService<NearExpiryNudgeSweepService>();

var app = builder.Build();

// One structured line per request (method, path, status, elapsed ms) — placed first so it also
// covers the UseExceptionHandler re-execution below.
app.UseSerilogRequestLogging();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcoMealDbContext>();
    await dbContext.Database.MigrateAsync();

    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Default HSTS max-age is 30 days; see https://aka.ms/aspnetcore-hsts to tune for production.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Package/business photos saved by ImageUploadService. MapStaticAssets below only serves the
// build-time asset manifest, so files written to wwwroot/uploads at runtime need this separate,
// always-on middleware instead — otherwise they'd 404 (or get swallowed by the Blazor Server
// fallback route) despite sitting right there on disk.
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();

// MapStaticAssets' default FileExtensionContentTypeProvider has no entry for .webmanifest, so it
// would otherwise fall back to application/octet-stream — some browsers refuse to treat that as
// an installable manifest.
app.MapGet("/manifest.webmanifest", () =>
    Results.File(Path.Combine(app.Environment.WebRootPath, "manifest.webmanifest"), "application/manifest+json"));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
