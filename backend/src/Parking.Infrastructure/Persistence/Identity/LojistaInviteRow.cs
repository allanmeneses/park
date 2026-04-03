using System.ComponentModel.DataAnnotations.Schema;

namespace Parking.Infrastructure.Persistence.Identity;

/// <summary>
/// Convite de auto cadastro de lojista: código público (merchant) + código de ativação (hash).
/// </summary>
[Table("lojista_invites")]
public sealed class LojistaInviteRow
{
    public Guid Id { get; set; }
    public Guid ParkingId { get; set; }
    public Guid LojistaId { get; set; }

    /// <summary>Código alfanumérico de 10 caracteres, único globalmente.</summary>
    public string MerchantCode { get; set; } = "";

    /// <summary>SHA-256 hexadecimal minúsculo do código de ativação em UTF-8.</summary>
    public string ActivationCodeHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public Guid? ActivatedUserId { get; set; }
}
