-- Apaga todas as bonificações (lojista_grants) no tenant.
-- Repõe horas em lojista_wallets. Ignora se a tabela não existir (BD tenant antigo).

DO $$
BEGIN
    IF to_regclass('public.lojista_grants') IS NULL THEN
        RETURN;
    END IF;

    UPDATE lojista_wallets lw
    SET balance_hours = lw.balance_hours + sub.h
    FROM (
        SELECT lojista_id, SUM(hours)::int AS h
        FROM lojista_grants
        GROUP BY lojista_id
    ) sub
    WHERE lw.lojista_id = sub.lojista_id;

    DELETE FROM lojista_grants;
END $$;
