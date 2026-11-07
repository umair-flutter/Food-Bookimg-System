using FoodBooking.Core.Data;
using FoodBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FoodBooking.Core.Services;

public class SettingsService(AppDbContext db, IMemoryCache cache)
{
    private const string Key = "site-settings";

    /// <summary>Cached read-only copy of the settings row (cache 60s so admin changes show up quickly on the front site).</summary>
    public async Task<SiteSetting> GetAsync()
    {
        if (cache.TryGetValue(Key, out SiteSetting? s) && s != null) return s;
        s = await db.SiteSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new SiteSetting();
        cache.Set(Key, s, TimeSpan.FromSeconds(60));
        return s;
    }

    /// <summary>Tracked settings row for editing in the admin panel.</summary>
    public async Task<SiteSetting> GetForEditAsync()
    {
        var s = await db.SiteSettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (s == null) { s = new SiteSetting(); db.SiteSettings.Add(s); await db.SaveChangesAsync(); }
        return s;
    }

    public void Invalidate() => cache.Remove(Key);
}

public record StoreStatus(bool IsOpen, string Message, Holiday? Holiday, TimeSpan? OpensAt, TimeSpan? ClosesAt);

/// <summary>Opening hours, holidays and store-local time.</summary>
public class StoreHoursService(AppDbContext db, SettingsService settings)
{
    public async Task<TimeZoneInfo> TimeZoneAsync()
    {
        var s = await settings.GetAsync();
        try { return TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId); }
        catch { return TimeZoneInfo.Utc; }
    }

    public async Task<DateTime> NowAsync() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, await TimeZoneAsync());

    public async Task<DateTime> ToLocalAsync(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), await TimeZoneAsync());

    /// <summary>Opening window for a given store-local date, or null when closed that day.</summary>
    public async Task<(TimeSpan open, TimeSpan close, Holiday? holiday)?> HoursForDateAsync(DateTime localDate)
    {
        var day = localDate.Date;
        var holiday = await db.Holidays.AsNoTracking().FirstOrDefaultAsync(h => h.Date == day);
        if (holiday != null)
        {
            if (holiday.IsClosedAllDay || holiday.OpenTime == null || holiday.CloseTime == null) return null;
            return (holiday.OpenTime.Value, holiday.CloseTime.Value, holiday);
        }
        var bh = await db.BusinessHours.AsNoTracking().FirstOrDefaultAsync(x => x.DayOfWeek == day.DayOfWeek);
        if (bh == null) return (TimeSpan.Zero, new TimeSpan(23, 59, 0), null); // no hours configured = always open
        if (bh.IsClosed) return null;
        return (bh.OpenTime, bh.CloseTime, null);
    }

    public async Task<bool> IsOpenAtAsync(DateTime localTime)
    {
        var hours = await HoursForDateAsync(localTime);
        if (hours == null) return false;
        var t = localTime.TimeOfDay;
        var (open, close, _) = hours.Value;
        return close > open ? t >= open && t <= close : t >= open || t <= close; // supports past-midnight closing
    }

    public async Task<StoreStatus> GetStatusAsync()
    {
        var now = await NowAsync();
        var holiday = await db.Holidays.AsNoTracking().FirstOrDefaultAsync(h => h.Date == now.Date);
        var hours = await HoursForDateAsync(now);
        if (hours == null)
        {
            var msg = holiday != null
                ? $"Closed today for {holiday.Title}. {holiday.Message}".Trim()
                : "We are closed today. You can still schedule an order for a later day.";
            return new StoreStatus(false, msg, holiday, null, null);
        }
        var open = await IsOpenAtAsync(now);
        var (o, c, _) = hours.Value;
        var text = open
            ? $"Open now · until {Fmt(c)}"
            : now.TimeOfDay < o ? $"Opens today at {Fmt(o)} · schedule your order now" : "Closed for today · schedule an order for later";
        return new StoreStatus(open, text, holiday, o, c);
    }

    /// <summary>Available pickup / delivery time slots (store-local) for the next few days.</summary>
    public async Task<List<DateTime>> GetTimeSlotsAsync(int days = 3, int slotMinutes = 30, int leadMinutes = 30)
    {
        var now = await NowAsync();
        var earliest = now.AddMinutes(leadMinutes);
        var slots = new List<DateTime>();
        for (int d = 0; d < days; d++)
        {
            var date = now.Date.AddDays(d);
            var hours = await HoursForDateAsync(date);
            if (hours == null) continue;
            var (open, close, _) = hours.Value;
            var end = close > open ? date + close : date.AddDays(1) + close;
            for (var t = date + open; t <= end; t = t.AddMinutes(slotMinutes))
                if (t >= earliest) slots.Add(t);
        }
        return slots;
    }

    public static string Fmt(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt");
}
