using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api;

/// <summary>
/// Saldo de horas bonificadas (convênio) por placa + lojista — ignora consumos <c>lojista</c> anteriores à
/// primeira bonificação em <c>lojista_grants</c>, que vinham do modelo antigo (débito automático da carteira).
/// </summary>
internal static class LojistaBonificadoBalance
{
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
