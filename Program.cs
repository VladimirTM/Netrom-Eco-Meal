using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Components;
using Netrom_Eco_Meal.Controllers;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;

// Single-locale app: prices are always RON, so every ToString("C") call site (cart,
// package cards, orders...) gets that formatting for free instead of the server's OS culture.
var romanianCulture = new CultureInfo("ro-RO");
CultureInfo.DefaultThreadCurrentCulture = romanianCulture;
CultureInfo.DefaultThreadCurrentUICulture = romanianCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var connectionString = builder.Configuration.GetConnectionString("EcoMealContext");
builder.Services.AddDbContext<EcoMealDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
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
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ClientTimeZoneService>();
builder.Services.AddControllers();
builder.Services.AddScoped<BusinessController>();
builder.Services.AddScoped<PackageController>();
builder.Services.AddScoped<UserController>();
builder.Services.AddScoped<OrderController>();
builder.Services.AddScoped<ReviewController>();

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
