-- SPEC v8.4 §21 — executar no banco do TENANT (parking_{uuid}) após migrations.
-- IDs fixos para documentação e testes; apps listam via GET /recharge-packages.

INSERT INTO recharge_packages (id, display_name, scope, hours, price, is_promo, sort_order, active) VALUES
  ('11111111-1111-1111-1111-111111111101', 'Cliente 10h', 'CLIENT',  10,  50.00, false, 10, true),
  ('11111111-1111-1111-1111-111111111102', 'Cliente Promo 50h', 'CLIENT',  50, 200.00, true, 20, true),
  ('22222222-2222-2222-2222-222222222201', 'Convênio 20h', 'LOJISTA', 20, 100.00, false, 10, true),
  ('22222222-2222-2222-2222-222222222202', 'Convênio Promo 100h', 'LOJISTA', 100, 400.00, true, 20, true)
ON CONFLICT (id) DO NOTHING;
