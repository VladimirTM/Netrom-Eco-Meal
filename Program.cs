using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Components;
using Netrom_Eco_Meal.Controllers;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("EcoMealContext");
builder.Services.AddDbContext<EcoMealDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IBusinessTypeRepository, BusinessTypeRepository>();
builder.Services.AddScoped<IBusinessTypeService, BusinessTypeService>();
builder.Services.AddControllers();
builder.Services.AddScoped<BusinessController>();

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