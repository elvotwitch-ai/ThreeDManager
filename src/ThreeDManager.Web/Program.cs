using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Application.Interfaces;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Infrastructure.Parsers;
using ThreeDManager.Infrastructure.Services;
using ThreeDManager.Web.ModelBinding;
using ThreeDManager.Web.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "ThreeDManager";
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider());
});

var bypassAuthentication = builder.Environment.IsEnvironment("Testing")
    && builder.Configuration.GetValue<bool>("Testing:BypassAuthentication");

builder.Services.Configure<AlphaAccessOptions>(
    builder.Configuration.GetSection(AlphaAccessOptions.SectionName));
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ThreeDManager.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
    });
builder.Services.AddAuthorization(options =>
{
    if (!bypassAuthentication)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    var alphaAccess = builder.Configuration
        .GetSection(AlphaAccessOptions.SectionName)
        .Get<AlphaAccessOptions>();

    if (string.IsNullOrWhiteSpace(alphaAccess?.Username)
        || string.IsNullOrWhiteSpace(alphaAccess.Password))
    {
        throw new InvalidOperationException(
            "Alpha access credentials are required. Configure AlphaAccess__Username and AlphaAccess__Password.");
    }
}

if (builder.Environment.IsEnvironment("Testing"))
{
    var databaseName = builder.Configuration["Testing:DatabaseName"] ?? "ThreeDManager.Tests";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase(databaseName));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Default"),
            npgsql => npgsql.EnableRetryOnFailure()));
}

builder.Services.AddScoped<IPrintFileParser, GCodePrintFileParser>();
builder.Services.AddScoped<IPrintJobStockService, PrintJobStockService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program { }
