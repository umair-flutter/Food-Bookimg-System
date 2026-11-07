using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace FoodBooking.Core.Services;

public class UploadOptions
{
    /// <summary>Physical folder shared by the Front and Admin sites, served at /uploads on both.</summary>
    public string PhysicalPath { get; set; } = "";
}

/// <summary>Saves uploaded images into the shared uploads folder (served at /uploads by both sites).</summary>
public class FileStorage(UploadOptions options)
{
    private static readonly string[] Allowed = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".ico"];
    public const long MaxBytes = 5 * 1024 * 1024;

    public string Root => options.PhysicalPath;

    public static string ResolveRoot(string? configured, IHostEnvironment env)
    {
        var path = string.IsNullOrWhiteSpace(configured) ? Path.Combine("..", "uploads") : configured;
        if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(env.ContentRootPath, path));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Returns the public URL ("/uploads/folder/file.ext") or throws InvalidOperationException for bad files.</summary>
    public async Task<string> SaveAsync(IFormFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) throw new InvalidOperationException("Only JPG, PNG, GIF, WEBP or ICO images are allowed.");
        if (file.Length == 0 || file.Length > MaxBytes) throw new InvalidOperationException("Image must be smaller than 5 MB.");
        folder = Slug.Create(folder);
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = File.Create(Path.Combine(dir, name)))
            await file.CopyToAsync(fs);
        return $"/uploads/{folder}/{name}";
    }

    public void Delete(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/")) return;
        var full = Path.GetFullPath(Path.Combine(Root, url["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar)));
        if (full.StartsWith(Path.GetFullPath(Root)) && File.Exists(full)) File.Delete(full);
    }

    /// <summary>Writes an SVG placeholder image (used for demo data) and returns its URL.</summary>
    public string WritePlaceholder(string folder, string fileName, string title, string emoji, string color1, string color2, int w = 800, int h = 600)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        var t = WebUtility.HtmlEncode(title);
        var fontSize = w >= 1200 ? 64 : 44;
        var svg = $"""
<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{color1}"/><stop offset="1" stop-color="{color2}"/></linearGradient>
    <radialGradient id="r" cx=".5" cy=".42" r=".45"><stop offset="0" stop-color="#fff" stop-opacity=".35"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>
  </defs>
  <rect width="{w}" height="{h}" fill="url(#g)"/>
  <circle cx="{w / 2}" cy="{h * 0.42}" r="{h * 0.33}" fill="url(#r)"/>
  <circle cx="{w * 0.12}" cy="{h * 0.85}" r="{h * 0.18}" fill="#fff" opacity=".08"/>
  <circle cx="{w * 0.9}" cy="{h * 0.12}" r="{h * 0.14}" fill="#fff" opacity=".08"/>
  <text x="50%" y="{h * 0.5}" font-size="{h * 0.33}" text-anchor="middle" dominant-baseline="middle">{emoji}</text>
  <text x="50%" y="{h * 0.86}" font-family="Segoe UI, Arial, sans-serif" font-weight="700" font-size="{fontSize}" fill="#fff" text-anchor="middle">{t}</text>
</svg>
""";
        File.WriteAllText(Path.Combine(dir, fileName), svg);
        return $"/uploads/{folder}/{fileName}";
    }
}
