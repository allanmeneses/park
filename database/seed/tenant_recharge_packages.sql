-- SPEC v8.4 §21 — executar no banco do TENANT (parking_{uuid}) após migrations.
-- IDs fixos para documentação e testes; apps listam via GET /recharge-packages.

INSERT INTO recharge_packages (id, scope, hours, price, active) VALUES
  ('11111111-1111-1111-1111-111111111101', 'CLIENT',  10,  50.00, true),
  ('11111111-1111-1111-1111-111111111102', 'CLIENT',  50, 200.00, true),
  ('22222222-2222-2222-2222-222222222201', 'LOJISTA', 20, 100.00, true),
  ('22222222-2222-2222-2222-222222222202', 'LOJISTA', 100, 400.00, true)
ON CONFLICT (id) DO NOTHING;
