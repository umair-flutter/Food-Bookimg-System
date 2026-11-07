using FoodBooking.Core.Data;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodBooking.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFoodBookingCore(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var cs = config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing in appsettings.json.");
        // Database:Provider = "Sqlite" runs a self-contained demo database (used for the hosted demo).
        var useSqlite = string.Equals(config["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
        services.AddDbContext<AppDbContext>(o =>
        {
            if (useSqlite) o.UseSqlite(config.GetConnectionString("Sqlite") ?? "Data Source=foodbooking.db",
                sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            else o.UseSqlServer(cs, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddSingleton(new UploadOptions { PhysicalPath = FileStorage.ResolveRoot(config["Uploads:PhysicalPath"], env) });
        services.AddSingleton<FileStorage>();
        services.AddScoped<SettingsService>();
        services.AddScoped<StoreHoursService>();
        services.AddScoped<PricingService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<CloverService>();
        services.AddScoped<PayPalService>();
        services.AddScoped<OrderService>();
        return services;
    }

    /// <summary>Serves the shared uploads folder at /uploads and creates/seeds the database.</summary>
    public static async Task UseFoodBookingCoreAsync(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<FileStorage>();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storage.Root),
            RequestPath = "/uploads"
        });

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await DbSeeder.InitializeAsync(db, storage);
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Database initialisation failed. Check the DefaultConnection string in appsettings.json.");
            throw;
        }
    }
}
