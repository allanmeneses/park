namespace Parking.Infrastructure.Tenants;

public static class TenantConnectionStringBuilder
{
    public static string FromTemplate(string template, Guid parkingId)
    {
        var n = parkingId.ToString("N");
        if (!template.Contains("{uuid}", StringComparison.Ordinal))
            throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE must contain {uuid}");
        return template.Replace("{uuid}", n, StringComparison.Ordinal);
    }
}
