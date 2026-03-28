using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api;

/// <summary>SPEC §18 — GET /client/history e /lojista/history (merge PURCHASE + USAGE).</summary>
internal static class WalletHistoryBuilder
{
    private const int MaxFetch = 500;

    public static async Task<object> BuildClientHistoryAsync(
        TenantDbContext db,
        Guid clientEntityId,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        TryParseCursor(cursor, out var cAt, out var cKind, out var cId);

        var purchases = await db.WalletLedger.AsNoTracking()
            .Where(w => w.ClientId == clientEntityId)
            .OrderByDescending(w => w.CreatedAt)
            .ThenByDescending(w => w.Id)
            .Take(MaxFetch)
            .ToListAsync(ct);

        var usageRows = await (
            from u in db.WalletUsages.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
            join c in db.Clients.AsNoTracking() on t.Plate equals c.Plate
            where c.Id == clientEntityId && u.Source == "client"
            select new UsageRow(u.Id, u.HoursUsed, t.Id, t.ExitTime ?? t.EntryTime)
        ).Take(MaxFetch).ToListAsync(ct);

        var merged = new List<Merged>();
        foreach (var w in purchases)
        {
            var refId = w.PackageId ?? w.Id;
            merged.Add(new Merged(w.Id, "PURCHASE", w.DeltaHours, w.Amount, w.CreatedAt, "package", refId));
        }

        foreach (var u in usageRows)
            merged.Add(new Merged(u.Id, "USAGE", -u.HoursUsed, 0m, u.Created, "ticket", u.TicketId));

        merged.Sort(static (a, b) => CompareNewerFirst(a, b));

        if (cAt is { } ca && cKind != null && cId is { } cid)
        {
            var needle = new Merged(cid, cKind, 0, 0, ca, "", Guid.Empty);
            merged = merged.Where(x => CompareNewerFirst(needle, x) < 0).ToList();
        }

        var hasMore = merged.Count > limit;
        var page = merged.Take(limit).ToList();
        string? next = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            next = EncodeCursor(last.CreatedAt, last.Kind, last.Id);
        }

        var items = page.Select(x => new
        {
            id = x.Id,
            kind = x.Kind,
            delta_hours = x.DeltaHours,
            amount = x.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            created_at = x.CreatedAt,
            @ref = new { type = x.RefType, id = x.RefId }
        }).ToList();

        return new { items, next_cursor = (string?)next };
    }

    public static async Task<object> BuildLojistaHistoryAsync(
        TenantDbContext db,
        Guid lojistaEntityId,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        TryParseCursor(cursor, out var cAt, out var cKind, out var cId);

        var purchases = await db.WalletLedger.AsNoTracking()
            .Where(w => w.LojistaId == lojistaEntityId)
            .OrderByDescending(w => w.CreatedAt)
            .ThenByDescending(w => w.Id)
            .Take(MaxFetch)
            .ToListAsync(ct);

        var usageRows = await (
            from u in db.WalletUsages.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
            join c in db.Clients.AsNoTracking() on t.Plate equals c.Plate
            where u.Source == "lojista" && c.LojistaId == lojistaEntityId
            select new UsageRow(u.Id, u.HoursUsed, t.Id, t.ExitTime ?? t.EntryTime)
        ).Take(MaxFetch).ToListAsync(ct);

        var merged = new List<Merged>();
        foreach (var w in purchases)
        {
            var refId = w.PackageId ?? w.Id;
            merged.Add(new Merged(w.Id, "PURCHASE", w.DeltaHours, w.Amount, w.CreatedAt, "package", refId));
        }

        foreach (var u in usageRows)
            merged.Add(new Merged(u.Id, "USAGE", -u.HoursUsed, 0m, u.Created, "ticket", u.TicketId));

        merged.Sort(static (a, b) => CompareNewerFirst(a, b));

        if (cAt is { } ca && cKind != null && cId is { } cid)
        {
            var needle = new Merged(cid, cKind, 0, 0, ca, "", Guid.Empty);
            merged = merged.Where(x => CompareNewerFirst(needle, x) < 0).ToList();
        }

        var hasMore = merged.Count > limit;
        var page = merged.Take(limit).ToList();
        string? next = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            next = EncodeCursor(last.CreatedAt, last.Kind, last.Id);
        }

        var items = page.Select(x => new
        {
            id = x.Id,
            kind = x.Kind,
            delta_hours = x.DeltaHours,
            amount = x.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            created_at = x.CreatedAt,
            @ref = new { type = x.RefType, id = x.RefId }
        }).ToList();

        return new { items, next_cursor = (string?)next };
    }

    private sealed record UsageRow(Guid Id, int HoursUsed, Guid TicketId, DateTimeOffset Created);

    private sealed record Merged(Guid Id, string Kind, int DeltaHours, decimal Amount, DateTimeOffset CreatedAt, string RefType, Guid RefId);

    /// <summary>Negativo se <paramref name="a"/> deve aparecer antes de <paramref name="b"/> (mais novo primeiro).</summary>
    private static int CompareNewerFirst(Merged a, Merged b)
    {
        var t = b.CreatedAt.CompareTo(a.CreatedAt);
        if (t != 0) return t;
        var rk = KindRank(b.Kind).CompareTo(KindRank(a.Kind));
        if (rk != 0) return rk;
        return b.Id.CompareTo(a.Id);
    }

    private static int KindRank(string k) =>
        k.Equals("USAGE", StringComparison.Ordinal) ? 2 : 1;

    private static string EncodeCursor(DateTimeOffset at, string kind, Guid id)
    {
        var raw = $"{at:o}|{kind}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }

    private static void TryParseCursor(string? cursor, out DateTimeOffset? at, out string? kind, out Guid? id)
    {
        at = null;
        kind = null;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor)) return;
        try
        {
            var padded = cursor.Replace("-", "+", StringComparison.Ordinal).Replace("_", "/", StringComparison.Ordinal);
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = json.Split('|', StringSplitOptions.None);
            if (parts.Length != 3) return;
            if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var a))
                return;
            if (!Guid.TryParse(parts[2], out var g)) return;
            at = a;
            kind = parts[1];
            id = g;
        }
        catch
        {
            /* ignore */
        }
    }
}
