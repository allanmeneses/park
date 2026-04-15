# Plano — Configuração PSP por tenant (evolução)

Documento vivo: requisitos e decisões para evoluir o sistema **sem regressão** do que já está em produção.

## Requisito não negociável (aceitação)

- **Hoje:** pagamento por **PIX** e **cartão de crédito** já funciona na aplicação (fluxos reais com PSP, conforme SPEC e código atual).
- **Depois das alterações:** esses fluxos **têm de continuar a funcionar** para todos os cenários já suportados (incluindo ambientes que ainda usam **configuração global** por variáveis de ambiente).
- O projeto está em **fase de evolução**: a base está **funcional e pronta para uso**; mudanças devem ser **incrementais e reversíveis** onde possível.

## Compatibilidade (obrigatória na implementação)

- **Fallback:** enquanto um tenant **não** tiver configuração PSP própria persistida (ou enquanto um flag explícito indicar “usar global”), o comportamento deve ser **idêntico ao atual** (credenciais e modo via `IConfiguration` / env: `PIX_MODE`, `MERCADOPAGO_*`, webhook stub global, etc.).
- **Após** o cliente configurar PSP no painel, esse tenant passa a usar **apenas** as credenciais do tenant (sandbox vs produção conforme SPEC/UI).
- **Testes:** regressão automática dos fluxos PIX e cartão existentes **antes e depois** do merge; CI verde obrigatório (ver `AGENTS.md`).

## Axiomas da construção (sempre)

Qualquer entrega desta evolução **não está completa** sem **todos** os itens abaixo. Isto alinha-se a `AGENTS.md`, `SPEC.md`, `SPEC_FRONTEND.md` e ao manual existente.

1. **TDD** — teste que falha primeiro, depois implementação mínima, depois refatorar (`SPEC.md` / `AGENTS.md`). Backend: `dotnet test` no projeto `*Tests*`; alterações em `frontend-web`: `npm run test` (e E2E quando aplicável à mudança); Android: `test` e, quando couber, testes alinhados ao CI (`connectedDebugAndroidTest` se tocar fluxos instrumentados). Merge só com evidência de testes verdes.
2. **Web** — paridade de negócio na SPA Vue: novos fluxos ou alterações refletidas em `frontend-web/`, com lint/build/test conforme pipeline.
3. **Android** — mesma regra de paridade em `android/` (Compose, rotas, textos em `UiStrings` / SPEC_FRONTEND).
4. **Specs** — `SPEC.md` (API, DDL, RBAC, regras) e `SPEC_FRONTEND.md` (UI, rotas, textos, acessos) atualizados **antes** ou em conjunto com o código; sem SPEC desatualizada.
5. **Manual do utilizador** — `docs/MANUAL_DO_USUARIO.md` (e, se aplicável, `docs/MANUAL_USUARIO_LOJISTA.md` ou secções afins) atualizados para administradores/operadores: onde configurar PSP, sandbox vs produção, responsabilidade das credenciais e impacto nos pagamentos.

## Decisões já tomadas (resumo)

| Área | Decisão |
|------|---------|
| Âmbito v1 | PIX + cartão (Mercado Pago; alinhar campos à SPEC) |
| Quem edita | ADMIN; SUPER_ADMIN com **motivo obrigatório** + auditoria |
| Segredos | Cifrados na BD do tenant |
| Auditoria | Por **campo** alterado; **nunca** logar valores secretos |
| UI legal | Checkbox obrigatório de responsabilidade |
| Webhook | URL com **`parking_id` no path** (padrão recomendado) |
| Superfícies | Web + Android |
| Sandbox | Obrigatório por tenant (teste antes de produção) |
| Cobrança da plataforma (SaaS ao dono) | **Fora** deste plano — trilho separado |

## Trilhos

1. **Trilho A (este documento):** PSP por estacionamento — motorista paga o operador.
2. **Trilho B (futuro):** Assinatura / produtividade — dono do estacionamento paga a plataforma.

## Próximo passo

Implementação só após ordem explícita de execução; antes: atualizar `SPEC.md` / `SPEC_FRONTEND.md` com RBAC, DDL, endpoints e regra de **fallback global** acima. A checklist de aceitação de cada PR inclui explicitamente os **Axiomas da construção** (TDD + Web + Android + specs + manual).
