using Microsoft.EntityFrameworkCore;
using ThreeDManager.Application.Interfaces;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Infrastructure.Parsers;
using ThreeDManager.Infrastructure.Services;
using ThreeDManager.Web.ModelBinding;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider());
});

if (builder.Environment.IsEnvironment("Testing"))
{
    var databaseName = builder.Configuration["Testing:DatabaseName"] ?? "ThreeDManager.Tests";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase(databaseName));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program { }
