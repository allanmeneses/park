using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parking.Infrastructure.Auth;
using Parking.Infrastructure.Persistence;
using Parking.Infrastructure.Persistence.Audit;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Payments;
using Parking.Infrastructure.Payments.MercadoPago;
using Parking.Infrastructure.Tenants;

namespace Parking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddParkingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(o =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(o);
            if (string.IsNullOrEmpty(o.Secret))
                o.Secret = configuration["JWT_SECRET"] ?? "";
        });

        var identityCs = configuration["DATABASE_URL_IDENTITY"]
                         ?? throw new InvalidOperationException("DATABASE_URL_IDENTITY is required");
        var auditCs = configuration["DATABASE_URL_AUDIT"]
                      ?? throw new InvalidOperationException("DATABASE_URL_AUDIT is required");

        services.AddScoped<IdentityDbContext>(_ =>
        {
            var b = new DbContextOptionsBuilder<IdentityDbContext>();
            NpgsqlEnumConfigurator.ConfigureIdentityNpgsql(b, identityCs);
            return new IdentityDbContext(b.Options);
        });
        services.AddScoped<AuditDbContext>(_ =>
        {
            var b = new DbContextOptionsBuilder<AuditDbContext>();
            NpgsqlEnumConfigurator.ConfigureAuditNpgsql(b, auditCs);
            return new AuditDbContext(b.Options);
        });

        services.AddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped<IOperatorProblemAuthCheck, OperatorProblemAuthCheck>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<AuditService>();
        services.AddScoped<TenantProvisioner>();
        services.AddScoped<PaymentWebhookSettlement>();

        services.Configure<MercadoPagoOptions>(o =>
        {
            configuration.GetSection(MercadoPagoOptions.SectionName).Bind(o);
            if (string.IsNullOrWhiteSpace(o.AccessToken))
                o.AccessToken = configuration["MERCADOPAGO_ACCESS_TOKEN"] ?? "";
            if (string.IsNullOrWhiteSpace(o.PublicKey))
                o.PublicKey = configuration["MERCADOPAGO_PUBLIC_KEY"] ?? "";
            if (string.IsNullOrWhiteSpace(o.WebhookSecret))
                o.WebhookSecret = (configuration["MERCADOPAGO_WEBHOOK_SECRET"] ?? "").Trim();
            else
                o.WebhookSecret = o.WebhookSecret.Trim();
            if (string.IsNullOrWhiteSpace(o.PayerEmail))
                o.PayerEmail = configuration["MERCADOPAGO_PAYER_EMAIL"] ?? "parking-payer@example.com";
            if (string.IsNullOrWhiteSpace(o.ApiBaseUrl))
                o.ApiBaseUrl = configuration["MERCADOPAGO_API_BASE_URL"] ?? "https://api.mercadopago.com";
            o.CheckoutBackSuccessUrl = configuration["MERCADOPAGO_CHECKOUT_BACK_SUCCESS_URL"];
            o.CheckoutBackFailureUrl = configuration["MERCADOPAGO_CHECKOUT_BACK_FAILURE_URL"];
            o.CheckoutBackPendingUrl = configuration["MERCADOPAGO_CHECKOUT_BACK_PENDING_URL"];
        });

        // Named client sem BaseAddress nem Bearer fixo: cada pedido usa URL e token efetivos (global ou tenant).
        services.AddHttpClient(nameof(MercadoPagoPaymentServiceProvider));

        services.AddSingleton<StubPaymentServiceProvider>();
        services.AddSingleton<TenantSecretProtector>();
        services.AddScoped<MercadoPagoTenantOptionsResolver>();
        services.AddScoped<IPaymentServiceProvider, TenantScopedPaymentServiceProvider>();

        return services;
    }
}
