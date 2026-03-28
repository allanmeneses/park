namespace Parking.Application.Checkout;

public static class CheckoutMath
{
    /// <summary>horas_total = CEIL(GREATEST(0, seconds/3600))</summary>
    public static int ComputeBillableHours(DateTimeOffset entry, DateTimeOffset exit)
    {
        var seconds = (exit - entry).TotalSeconds;
        if (seconds <= 0) return 0;
        return (int)Math.Ceiling(seconds / 3600.0);
    }

    /// <summary>Half-up rounding to 2 decimals (banker's not used — PostgreSQL ROUND numeric).</summary>
    public static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
