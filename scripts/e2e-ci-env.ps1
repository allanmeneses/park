# Variáveis do job "Frontend E2E" em .github/workflows/ci.yml (frontend-e2e).
# Um único lugar para alinhar local ↔ CI. Se mudar o workflow, atualize aqui.

@{
    Api = @{
        ASPNETCORE_URLS              = 'http://127.0.0.1:8080'
        DATABASE_URL_IDENTITY        = 'Host=127.0.0.1;Port=5432;Database=parking_identity;Username=parking;Password=parking_dev'
        DATABASE_URL_AUDIT           = 'Host=127.0.0.1;Port=5432;Database=parking_audit;Username=parking;Password=parking_dev'
        TENANT_DATABASE_URL_TEMPLATE = 'Host=127.0.0.1;Port=5432;Database=parking_{uuid};Username=parking;Password=parking_dev'
        JWT_SECRET                   = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        PIX_WEBHOOK_SECRET           = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
        PIX_MODE                     = 'Stub'
        E2E_SEED                     = '1'
        CORS_ORIGINS                 = 'http://127.0.0.1:5173'
    }
    Playwright = @{
        CI                 = 'true'
        E2E_API_ORIGIN     = 'http://127.0.0.1:8080'
        E2E_API_BASE       = 'http://127.0.0.1:8080/api/v1'
        PIX_WEBHOOK_SECRET = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
    }
}
