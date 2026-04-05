-- Zera o histórico de consumos em checkouts (wallet_usages) num tenant.
-- Antes de apagar, devolve à client_wallets as horas debitadas com source = 'client',
-- para não ficar saldo comprado "perdido".
--
-- NÃO apaga: tickets, pagamentos, lojista_grants, wallet_ledger, idempotency_store.
-- Uso: psql ou docker exec (ver clear-tenant-wallet-usages.ps1).

BEGIN;

UPDATE client_wallets cw
SET balance_hours = cw.balance_hours + sub.h
FROM (
    SELECT c.id AS client_id, SUM(w.hours_used)::int AS h
    FROM wallet_usages w
    INNER JOIN tickets t ON t.id = w.ticket_id
    INNER JOIN clients c ON c.plate = t.plate
    WHERE w.source = 'client'
    GROUP BY c.id
) sub
WHERE cw.client_id = sub.client_id;

DELETE FROM wallet_usages;

COMMIT;
