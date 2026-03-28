namespace Parking.Domain;

public enum UserRole
{
    OPERATOR,
    MANAGER,
    ADMIN,
    CLIENT,
    LOJISTA,
    SUPER_ADMIN
}

public enum TicketStatus
{
    OPEN,
    AWAITING_PAYMENT,
    CLOSED
}

public enum PaymentStatus
{
    PENDING,
    PAID,
    FAILED,
    EXPIRED
}

public enum PaymentMethod
{
    PIX,
    CARD,
    CASH
}

public enum CashSessionStatus
{
    OPEN,
    CLOSED
}

public enum PackageOrderStatus
{
    AWAITING_PAYMENT,
    PAID,
    FAILED,
    CANCELLED
}

public enum PackageScope
{
    CLIENT,
    LOJISTA
}

public enum SettlementKind
{
    PIX,
    CREDIT
}
