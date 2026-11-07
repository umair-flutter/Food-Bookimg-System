using FoodBooking.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFoodBookingCore(builder.Configuration, builder.Environment);
builder.Services.AddControllersWithViews(o =>
{
    // Everything in the admin panel requires a logged-in administrator unless marked [AllowAnonymous]
    o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = ".FoodBooking.Admin";
        o.LoginPath = "/auth/login";
        o.LogoutPath = "/auth/logout";
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
    });
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 50 * 1024 * 1024);

var app = builder.Build();

// PathBase (e.g. "/admin") lets the app run under a sub-path behind a reverse proxy.
if (app.Configuration["PathBase"] is { Length: > 0 } pathBase) app.UsePathBase(pathBase);

// `dotnet run -- --export-sql <file>` writes the database CREATE script (database/FoodBookingDb.sql) and exits.
var exportIdx = Array.IndexOf(args, "--export-sql");
if (exportIdx >= 0)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FoodBooking.Core.Data.AppDbContext>();
    var path = exportIdx + 1 < args.Length ? args[exportIdx + 1] : "FoodBookingDb.sql";
    File.WriteAllText(path, Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GenerateCreateScript(db.Database));
    Console.WriteLine("SQL script written to " + Path.GetFullPath(path));
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/dashboard/error");
    app.UseHsts();
}
app.UseHttpsRedirection();
await app.UseFoodBookingCoreAsync();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
