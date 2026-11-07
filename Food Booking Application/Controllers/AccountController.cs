using System.Security.Claims;
using System.Security.Cryptography;
using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Food_Booking_Application.Infrastructure;
using Food_Booking_Application.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Food_Booking_Application.Controllers;

[Route("account")]
public class AccountController(AppDbContext db, NotificationService notify, SettingsService settings, OrderService orders, CartStore cart) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.CustomerId() != null) return LocalRedirect(SafeReturn(returnUrl));
        ViewData["Title"] = "Login";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        ViewData["Title"] = "Login";
        if (!ModelState.IsValid) return View(vm);
        var email = vm.Email.Trim().ToLowerInvariant();
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email);
        if (c == null || !Passwords.Verify(c.PasswordHash, vm.Password))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(vm);
        }
        if (!c.IsActive)
        {
            ModelState.AddModelError("", "Your account has been disabled. Please contact us.");
            return View(vm);
        }
        c.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await SignIn(c, vm.RememberMe);
        TempData["Success"] = $"Welcome back, {c.FirstName}!";
        return LocalRedirect(SafeReturn(vm.ReturnUrl));
    }

    [HttpGet("register")]
    public IActionResult Register(string? returnUrl)
    {
        ViewData["Title"] = "Create Account";
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        ViewData["Title"] = "Create Account";
        if (!ModelState.IsValid) return View(vm);
        var email = vm.Email.Trim().ToLowerInvariant();
        if (await db.Customers.AnyAsync(x => x.Email == email))
        {
            ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
            return View(vm);
        }
        var c = new Customer
        {
            FirstName = vm.FirstName.Trim(), LastName = vm.LastName?.Trim(), Email = email, Phone = vm.Phone.Trim(),
            PasswordHash = Passwords.Hash(vm.Password), LastLoginAt = DateTime.UtcNow
        };
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        await SignIn(c, true);

        var s = await settings.GetAsync();
        await notify.SendEmailAsync(c.Email, $"Welcome to {s.CompanyName}!",
            $"<div style='font-family:Arial'><h2>Welcome, {System.Net.WebUtility.HtmlEncode(c.FirstName)}!</h2><p>Your account at {System.Net.WebUtility.HtmlEncode(s.CompanyName)} is ready. Enjoy ordering!</p></div>");
        TempData["Success"] = "Your account has been created.";
        return LocalRedirect(SafeReturn(vm.ReturnUrl));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "You have been logged out.";
        return Redirect("/");
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot Password";
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
    {
        ViewData["Title"] = "Forgot Password";
        if (!ModelState.IsValid) return View(vm);
        var email = vm.Email.Trim().ToLowerInvariant();
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email && x.IsActive);
        if (c != null)
        {
            c.ResetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            c.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(2);
            await db.SaveChangesAsync();
            var s = await settings.GetAsync();
            var link = $"{Request.Scheme}://{Request.Host}/account/reset-password?email={Uri.EscapeDataString(c.Email)}&token={c.ResetToken}";
            await notify.SendEmailAsync(c.Email, $"Reset your {s.CompanyName} password",
                $"<div style='font-family:Arial'><h2>Password reset</h2><p>Click the link below to choose a new password. The link is valid for 2 hours.</p><p><a href='{link}' style='background:#e63946;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none'>Reset password</a></p><p>If you didn't request this, you can ignore this email.</p></div>");
        }
        // Same response whether or not the email exists (prevents account enumeration)
        TempData["Success"] = "If an account exists for that email, a password reset link has been sent.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string email, string token)
    {
        ViewData["Title"] = "Reset Password";
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        ViewData["Title"] = "Reset Password";
        if (!ModelState.IsValid) return View(vm);
        var email = vm.Email.Trim().ToLowerInvariant();
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email);
        if (c == null || c.ResetToken == null || c.ResetTokenExpiresAt < DateTime.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(c.ResetToken), System.Text.Encoding.UTF8.GetBytes(vm.Token)))
        {
            ModelState.AddModelError("", "This reset link is invalid or has expired. Please request a new one.");
            return View(vm);
        }
        c.PasswordHash = Passwords.Hash(vm.Password);
        c.ResetToken = null; c.ResetTokenExpiresAt = null;
        await db.SaveChangesAsync();
        TempData["Success"] = "Your password has been reset. You can now log in.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize, HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var c = await Current();
        if (c == null) return await ForceLogout();
        ViewData["Title"] = "My Account";
        ViewBag.Orders = await db.Orders.AsNoTracking().Where(o => o.CustomerId == c.Id).OrderByDescending(o => o.CreatedAt).Take(5).ToListAsync();
        ViewBag.OrderCount = await db.Orders.CountAsync(o => o.CustomerId == c.Id);
        ViewBag.TotalSpent = await db.Orders.Where(o => o.CustomerId == c.Id && o.Status != OrderStatus.Cancelled).SumAsync(o => (decimal?)o.Total) ?? 0;
        return View(c);
    }

    [Authorize, HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var c = await Current();
        if (c == null) return await ForceLogout();
        ViewData["Title"] = "My Profile";
        return View(new ProfileViewModel
        {
            FirstName = c.FirstName, LastName = c.LastName, Phone = c.Phone ?? "", Email = c.Email,
            Address = c.Address, City = c.City, PostalCode = c.PostalCode
        });
    }

    [Authorize, HttpPost("profile")]
    public async Task<IActionResult> Profile(ProfileViewModel vm)
    {
        var c = await Current();
        if (c == null) return await ForceLogout();
        ViewData["Title"] = "My Profile";
        vm.Email = c.Email;
        if (!ModelState.IsValid) return View(vm);
        c.FirstName = vm.FirstName.Trim(); c.LastName = vm.LastName?.Trim(); c.Phone = vm.Phone.Trim();
        c.Address = vm.Address; c.City = vm.City; c.PostalCode = vm.PostalCode;
        await db.SaveChangesAsync();
        await SignIn(c, true);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpGet("change-password")]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Change Password";
        return View(new ChangePasswordViewModel());
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        ViewData["Title"] = "Change Password";
        if (!ModelState.IsValid) return View(vm);
        var c = await Current();
        if (c == null) return await ForceLogout();
        if (!Passwords.Verify(c.PasswordHash, vm.CurrentPassword))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Current password is incorrect.");
            return View(vm);
        }
        c.PasswordHash = Passwords.Hash(vm.NewPassword);
        await db.SaveChangesAsync();
        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpGet("orders")]
    public async Task<IActionResult> Orders(string? status, int page = 1)
    {
        var id = User.CustomerId();
        var q = db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.CustomerId == id);
        if (status == "active") q = q.Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);
        else if (status == "delivered") q = q.Where(o => o.Status == OrderStatus.Delivered);
        else if (status == "cancelled") q = q.Where(o => o.Status == OrderStatus.Cancelled);
        const int size = 10;
        var total = await q.CountAsync();
        ViewBag.Page = Math.Max(1, page); ViewBag.Pages = (int)Math.Ceiling(total / (double)size); ViewBag.Status = status;
        ViewData["Title"] = "Order History";
        return View(await q.OrderByDescending(o => o.CreatedAt).Skip((Math.Max(1, page) - 1) * size).Take(size).ToListAsync());
    }

    [Authorize, HttpGet("orders/{number}")]
    public async Task<IActionResult> OrderDetail(string number)
    {
        var id = User.CustomerId();
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.History)
            .FirstOrDefaultAsync(x => x.OrderNumber == number && x.CustomerId == id);
        if (o == null) return NotFound();
        ViewData["Title"] = "Order #" + o.OrderNumber;
        return View(o);
    }

    [Authorize, HttpPost("orders/{number}/cancel")]
    public async Task<IActionResult> Cancel(string number)
    {
        var id = User.CustomerId();
        var o = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderNumber == number && x.CustomerId == id);
        if (o == null) return NotFound();
        if (o.Status != OrderStatus.Pending)
            TempData["Error"] = "This order is already being prepared and can no longer be cancelled online. Please call us.";
        else
        {
            await orders.UpdateStatusAsync(o.Id, OrderStatus.Cancelled, "Cancelled by customer.", true);
            TempData["Success"] = "Your order has been cancelled." + (o.PaymentStatus == PaymentStatus.Paid ? " Your refund will be processed shortly." : "");
        }
        return RedirectToAction(nameof(OrderDetail), new { number });
    }

    /// <summary>Adds all still-available items of a previous order back to the cart.</summary>
    [Authorize, HttpPost("orders/{number}/reorder")]
    public async Task<IActionResult> Reorder(string number)
    {
        var id = User.CustomerId();
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.Product!)
                .ThenInclude(p => p.Options).ThenInclude(op => op.AttributeValue!).ThenInclude(v => v.Attribute)
            .FirstOrDefaultAsync(x => x.OrderNumber == number && x.CustomerId == id);
        if (o == null) return NotFound();
        var c = cart.Get();
        int added = 0;
        foreach (var i in o.Items.Where(i => i.Product is { IsActive: true }))
        {
            // Pick the default value for every required option group (e.g. Size)
            var defaults = i.Product!.Options
                .GroupBy(op => op.AttributeValue!.AttributeId)
                .Where(g => g.First().AttributeValue!.Attribute!.IsRequired)
                .Select(g => (g.FirstOrDefault(x => x.IsDefault) ?? g.First()).Id)
                .ToList();
            c.Lines.Add(new CartLine { Key = Guid.NewGuid().ToString("N")[..10], ProductId = i.ProductId!.Value, Quantity = i.Quantity, OptionIds = defaults });
            added++;
        }
        cart.Save(c);
        TempData[added > 0 ? "Success" : "Error"] = added > 0
            ? $"{added} item(s) added to your cart. Options were reset to defaults — please review before checkout."
            : "None of the items from that order are available any more.";
        return RedirectToAction("Index", "Cart");
    }

    private async Task<Customer?> Current() => User.CustomerId() is int id ? await db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.IsActive) : null;

    private async Task<IActionResult> ForceLogout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignIn(Customer c, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, c.Id.ToString()),
            new(ClaimTypes.Name, c.FirstName),
            new(ClaimTypes.Email, c.Email)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = persistent });
    }

    private string SafeReturn(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : "/account";
}
