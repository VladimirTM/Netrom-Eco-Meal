using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using NetromEcoMeal.Api.Hubs;
using NetromEcoMeal.Api.Middleware;
using NetromEcoMeal.Api.Services;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.AI;
using NetromEcoMeal.Services.Email;
using NetromEcoMeal.Services.Interfaces;
using NetromEcoMeal.Services.Payments;
using OllamaSharp;
using Serilog;

// Single-locale app: prices are always RON, so every ToString("C") call site gets that
// formatting for free instead of the server's OS culture.
var romanianCulture = new CultureInfo("ro-RO");
CultureInfo.DefaultThreadCurrentCulture = romanianCulture;
CultureInfo.DefaultThreadCurrentUICulture = romanianCulture;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId());

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"];
if (!string.IsNullOrWhiteSpace(ollamaBaseUrl))
{
    var ollamaModelId = builder.Configuration["Ollama:ModelId"] ?? "qwen2.5:7b";
    // OllamaApiClient(Uri, string) builds its own HttpClient internally with HttpClient's default
    // 100s Timeout — nowhere near enough for a tool-calling round-trip (search, then a JSON-schema
    // basket proposal) against this model under CPU-only inference, which alone can take 100s+
    // just to process one large prompt. Constructing the HttpClient ourselves with a generous
    // timeout is the only way to give slow local inference room to finish instead of the request
    // being cancelled mid-generation (surfaced as a raw OperationCanceledException, nothing logged).
    var ollamaHttpClient = new HttpClient { BaseAddress = new Uri(ollamaBaseUrl), Timeout = TimeSpan.FromMinutes(5) };
    builder.Services.AddSingleton<IChatClient>(new OllamaApiClient(ollamaHttpClient, ollamaModelId));
}

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// ImpactController's widget route opts into this explicitly (it's fetched cross-origin by
// impact-widget.js, embedded on a business's own website) — everything else goes through
// FrontendPolicy below.
builder.Services.AddCors(options =>
{
    options.AddPolicy("PublicImpactWidget", policy => policy.AllowAnyOrigin().WithMethods("GET"));

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173", "http://localhost:5174"];
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});

var connectionString = builder.Configuration.GetConnectionString("EcoMealContext");
builder.Services.AddDbContext<EcoMealDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", false);
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<EcoMealDbContext>()
    .AddDefaultTokenProviders();

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
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
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
builder.Services.AddScoped<IBasketPlannerAgent, BasketPlannerAgent>();
builder.Services.AddScoped<IMarkdownPricingAgent, MarkdownPricingAgent>();
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IRescueCircleService, RescueCircleService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IStandingOrderRepository, StandingOrderRepository>();
builder.Services.AddScoped<IStandingOrderService, StandingOrderService>();
builder.Services.AddScoped<IKitchenTipRepository, KitchenTipRepository>();
builder.Services.AddScoped<IKitchenTipService, KitchenTipService>();
builder.Services.AddScoped<IReferralRepository, ReferralRepository>();
builder.Services.AddScoped<IStoreCreditRepository, StoreCreditRepository>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddScoped<IBrandRepository, BrandRepository>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IWebhookIntakeService, WebhookIntakeService>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IPackageStockNotifier, SignalRPackageStockNotifier>();

// On by default. The flag stays so scaling the Api to several replicas doesn't sweep orders and
// generate templates once per replica.
if (builder.Configuration.GetValue("BackgroundJobs:Enabled", false))
{
    builder.Services.AddHostedService<OrderLifecycleSweepService>();
    builder.Services.AddHostedService<PackageTemplateGenerationService>();
    builder.Services.AddHostedService<NearExpiryNudgeSweepService>();
}

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters (256 bits) for HMAC-SHA256.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        options.Events = new JwtBearerEvents
        {
            // A browser's native WebSocket API can't set an Authorization header on the
            // handshake request, so @microsoft/signalr instead appends the token as
            // ?access_token=... — read it from there for hub requests only, never for ordinary
            // REST calls (which still require the real header).
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
            // JWTs can't be revoked directly — reject any token whose security_stamp claim no
            // longer matches the DB, so a password change invalidates every previously-issued
            // token immediately.
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;
                if (userId is null || tokenStamp is null)
                {
                    context.Fail("Invalid token.");
                    return;
                }

                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId);
                if (user is null || user.SecurityStamp != tokenStamp)
                    context.Fail("Token has been invalidated.");
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "NetromEcoMeal API v1"));
}

// The Api owns migrations and seeding.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcoMealDbContext>();
    await dbContext.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (!app.Environment.IsDevelopment())
    {
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    }
    await next();
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

// Package/business photos saved by ImageUploadService — MapStaticAssets only serves the
// build-time asset manifest, so runtime uploads need their own always-on static-file middleware
// pointed at the same folder (shared with the host via a Docker volume, so uploads survive an
// image rebuild). WebRootPath can be null if the host's content root has no wwwroot folder (e.g. the
// WebApplicationFactory test host, which doesn't publish one) — fall back to content root/wwwroot.
var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrEmpty(webRootPath))
    webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var uploadsPath = Path.Combine(webRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
});

// wwwroot/js/impact-widget.js — embedded by businesses on their own sites (D5, the embed URL
// doesn't change across the cutover). The widget infers its own fetch base URL from the <script>
// tag's src origin, so serving it from here (rather than the frontend's static assets) keeps that
// origin equal to the API's — exactly what Dashboard's "Share your impact" snippet already builds
// (`${apiOrigin}/js/impact-widget.js`).
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(webRootPath, "js")),
    RequestPath = "/js",
});

app.UseCors("FrontendPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<StockHub>("/hubs/stock");

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration test project.
public partial class Program;
