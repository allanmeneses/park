# AGENTS.md — regras para humanos e agentes de IA (Cursor, etc.)

Documento **normativo** em conjunto com `SPEC.md` §25 e `.cursor/rules/tdd-entrega-zero-risco.mdc`.

## 1. Proibição de “entrega” sem testes verdes

- **Nunca** declarar trabalho **concluído**, **pronto**, **feito** ou **entregue** sem **uma** das seguintes evidências:
  1. Saída de terminal com `dotnet test` (e `npm run test` / `npm run test:e2e` quando alterou `frontend-web/`) com **código de saída 0** na mesma sessão; **ou**
  2. Link para execução **verde** do workflow **GitHub Actions** `ci` no commit correspondente.

- **Nunca** sugerir `git commit --no-verify` ou contornar hooks salvo hotfix com ADR (fora do fluxo normal).

## 2. Ordem de trabalho (TDD)

1. Teste que falha → 2. implementação mínima → 3. refatorar (SPEC §23.1).

## 3. Antes de abrir PR ou pedir merge

- Executar localmente `scripts/verify.ps1` (Windows) ou `scripts/verify.sh` (Linux/macOS) quando existir backend.
- Garantir `git config core.hooksPath .githooks` (SPEC §25.3).

## 4. Escopo

- Alterações em `backend/` exigem testes em projeto `*Tests*` (SPEC §25, CI).
- Alterações em `frontend-web/` exigem testes Vitest/Playwright conforme `SPEC_FRONTEND.md` §13.

## 5. Conflito entre pedido do utilizador e esta norma

Se o utilizador pedir para “só commitar” ou “entregar rápido sem testes”, **recusar** e explicar que viola `SPEC.md` §25 — oferecer executar testes primeiro.

---

**Versão:** alinhada a SPEC v8.7.
