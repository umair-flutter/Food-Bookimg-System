using System.Security.Claims;
using FoodBooking.Admin.Models;
using FoodBooking.Core.Data;
using FoodBooking.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Admin.Controllers;

/// <summary>Shared helpers for admin controllers.</summary>
public abstract class AdminController : Controller
{
    protected void Success(string msg) => TempData["Success"] = msg;
    protected void Error(string msg) => TempData["Error"] = msg;
    protected int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : 0;

    /// <summary>Saves an uploaded image; on failure adds a model error and returns null.</summary>
    protected async Task<string?> TryUpload(FileStorage storage, IFormFile? file, string folder, string field)
    {
        if (file == null || file.Length == 0) return null;
        try { return await storage.SaveAsync(file, folder); }
        catch (InvalidOperationException ex) { ModelState.AddModelError(field, ex.Message); return null; }
    }
}

public class AuthController(AppDbContext db) : AdminController
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl) =>
        User.Identity?.IsAuthenticated == true ? Redirect("/") : View(new LoginModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == m.Username.Trim());
        if (user == null || !user.IsActive || !Passwords.Verify(user.PasswordHash, m.Password))
        {
            await Task.Delay(500); // slow down brute force
            ModelState.AddModelError("", "Invalid username or password.");
            return View(m);
        }
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName ?? user.Username)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(!string.IsNullOrEmpty(m.ReturnUrl) && Url.IsLocalUrl(m.ReturnUrl) ? m.ReturnUrl : "/");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var user = await db.AdminUsers.FindAsync(AdminId);
        if (user == null || !Passwords.Verify(user.PasswordHash, m.CurrentPassword))
        {
            ModelState.AddModelError(nameof(m.CurrentPassword), "Current password is incorrect.");
            return View(m);
        }
        user.PasswordHash = Passwords.Hash(m.NewPassword);
        await db.SaveChangesAsync();
        Success("Password changed successfully.");
        return Redirect("/");
    }
}
