using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api;

/// <summary>
/// Saldo de horas bonificadas (convênio) por placa + lojista — ignora consumos <c>lojista</c> anteriores à
/// primeira bonificação em <c>lojista_grants</c>, que vinham do modelo antigo (débito automático da carteira).
/// </summary>
internal static class LojistaBonificadoBalance
{
    internal sealed record LojistaBenefitItem(Guid LojistaId, string LojistaName, int HoursAvailable, int HoursGrantedTotal);

    /// <summary>
    /// Horas bonificadas (convênio) ainda disponíveis para a <paramref name="plate"/> — soma de todas as
    /// bonificações em <c>lojista_grants</c> menos os usos <c>lojista</c> em tickets com saída em/depois da
    /// primeira bonificação na placa. O checkout consome deste saldo <b>antes</b> da carteira comprada.
    /// </summary>
    internal static async Task<int> PlateAvailableBonificadoHoursAsync(TenantDbContext db, string plate, CancellationToken ct)
    {
        var hasGrants = await db.LojistaGrants.AsNoTracking().AnyAsync(g => g.Plate == plate, ct);
        if (!hasGrants)
            return 0;

        var totalGranted = await db.LojistaGrants.AsNoTracking()
            .Where(g => g.Plate == plate)
            .SumAsync(g => g.Hours, ct);

        var earliestGrantUtc = await db.LojistaGrants.AsNoTracking()
            .Where(g => g.Plate == plate)
            .MinAsync(g => (DateTimeOffset?)g.CreatedAt, ct);

        var totalUsed = await SumLojistaSourceUsagesForPlateAfterAsync(db, plate, earliestGrantUtc, ct);
        return Available(totalGranted, totalUsed);
    }

    /// <summary>
    /// Placas com horas bonificadas (convênio) ainda disponíveis &gt; 0 — mesma regra que o checkout
    /// (<see cref="PlateAvailableBonificadoHoursAsync"/>). Opcionalmente filtra por substring na placa (já normalizada).
    /// </summary>
    internal static async Task<IReadOnlyList<(string Plate, int BalanceHours)>> ListPlatesPositiveBonificadoAsync(
        TenantDbContext db,
        string? plateContainsNormalized,
        CancellationToken ct)
    {
        var distinctPlates = await db.LojistaGrants.AsNoTracking()
            .Select(g => g.Plate)
            .Distinct()
            .ToListAsync(ct);

        var rows = new List<(string Plate, int BalanceHours)>();
        foreach (var plate in distinctPlates)
        {
            if (plateContainsNormalized is { Length: > 0 } sub && !plate.Contains(sub, StringComparison.Ordinal))
                continue;

            var h = await PlateAvailableBonificadoHoursAsync(db, plate, ct);
            if (h > 0)
                rows.Add((plate, h));
        }

        return rows
            .OrderByDescending(x => x.BalanceHours)
            .ThenBy(x => x.Plate, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Itens para o resumo do ticket: um por lojista com bonificação na placa e
    /// <see cref="LojistaBenefitItem.HoursAvailable"/> &gt; 0 (sem bloco quando não há saldo bonificado a aplicar).
    /// Vários lojistas: uso repartido de forma <b>proporcional</b> ao concedido (só exibição; o checkout usa
    /// <see cref="PlateAvailableBonificadoHoursAsync"/>).
    /// </summary>
    internal static async Task<IReadOnlyList<LojistaBenefitItem>> ListBenefitsVisibleOnTicketAsync(
        TenantDbContext db, string plate, CancellationToken ct)
    {
        var distinctLojistaIds = await db.LojistaGrants.AsNoTracking()
            .Where(g => g.Plate == plate)
            .Select(g => g.LojistaId)
            .Distinct()
            .ToListAsync(ct);

        if (distinctLojistaIds.Count == 0)
            return [];

        var earliestGrantUtc = await db.LojistaGrants.AsNoTracking()
            .Where(g => g.Plate == plate)
            .MinAsync(g => (DateTimeOffset?)g.CreatedAt, ct);

        var totalUsedPlate = await SumLojistaSourceUsagesForPlateAfterAsync(db, plate, earliestGrantUtc, ct);

        var rows = new List<(Guid Lid, int Granted, string Name)>();
        foreach (var lid in distinctLojistaIds)
        {
            var granted = await SumGrantedHoursAsync(db, lid, plate, ct);
            if (granted <= 0)
                continue;
            var loj = await db.Lojistas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid, ct);
            rows.Add((lid, granted, loj?.Name ?? ""));
        }

        if (rows.Count == 0)
            return [];

        var totalGranted = rows.Sum(r => r.Granted);
        var ordered = rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var items = new List<LojistaBenefitItem>();
        foreach (var (lid, granted, name) in ordered)
        {
            int avail;
            if (distinctLojistaIds.Count == 1)
            {
                var used = await SumGrantScopedLojistaUsedHoursAsync(db, lid, plate, ct);
                avail = Available(granted, used);
            }
            else
            {
                var attributed = totalGranted == 0
                    ? 0
                    : (int)Math.Round((double)totalUsedPlate * granted / totalGranted, MidpointRounding.AwayFromZero);
                avail = Math.Max(0, granted - attributed);
            }

            if (avail > 0)
                items.Add(new LojistaBenefitItem(lid, name, avail, granted));
        }

        return items;
    }

    private static async Task<int> SumLojistaSourceUsagesForPlateAfterAsync(
        TenantDbContext db, string plate, DateTimeOffset? earliestGrantUtc, CancellationToken ct)
    {
        if (earliestGrantUtc is null)
            return 0;

        return await (
                from u in db.WalletUsages.AsNoTracking()
                join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
                where u.Source == "lojista"
                      && t.Plate == plate
                      && t.ExitTime != null
                      && t.ExitTime >= earliestGrantUtc
                select (int?)u.HoursUsed)
            .SumAsync(ct) ?? 0;
    }

    internal static Task<int> SumGrantedHoursAsync(
        TenantDbContext db, Guid lojistaId, string plate, CancellationToken ct) =>
        db.LojistaGrants.AsNoTracking()
            .Where(x => x.LojistaId == lojistaId && x.Plate == plate)
            .SumAsync(x => x.Hours, ct);

    internal static async Task<DateTimeOffset?> FirstGrantUtcAsync(
        TenantDbContext db, Guid lojistaId, string plate, CancellationToken ct)
    {
        return await db.LojistaGrants.AsNoTracking()
            .Where(x => x.LojistaId == lojistaId && x.Plate == plate)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (DateTimeOffset?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Sem bonificação registada ainda: não subtrair usos <c>lojista</c> antigos (legado).
    /// Com bonificação: só usos em tickets com saída em/depois da primeira bonificação.
    /// </summary>
    internal static async Task<int> SumGrantScopedLojistaUsedHoursAsync(
        TenantDbContext db, Guid lojistaId, string plate, CancellationToken ct)
    {
        var firstGrantUtc = await FirstGrantUtcAsync(db, lojistaId, plate, ct);
        if (firstGrantUtc is null)
            return 0;

        return await (
                from u in db.WalletUsages.AsNoTracking()
                join t in db.Tickets.AsNoTracking() on u.TicketId equals t.Id
                where u.Source == "lojista"
                      && t.Plate == plate
                      && t.ExitTime != null
                      && t.ExitTime >= firstGrantUtc
                select (int?)u.HoursUsed)
            .SumAsync(ct) ?? 0;
    }

    internal static int Available(int grantedHoursTotal, int grantScopedUsedHours) =>
        Math.Max(0, grantedHoursTotal - grantScopedUsedHours);
}
