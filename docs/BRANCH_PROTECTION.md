# Branch protection (GitHub) — SPEC v8.7 §25.5

## Automação (recomendado)

Com [GitHub CLI](https://cli.github.com/) autenticado (`gh auth login` com permissão `repo`):

- **PowerShell:** `.\scripts\setup-branch-protection.ps1`  
  - Repositório **só seu:** `.\scripts\setup-branch-protection.ps1 -Approvals 0` (sem revisão obrigatória de outra pessoa).
- **Bash:** `chmod +x scripts/setup-branch-protection.sh && ./scripts/setup-branch-protection.sh main 0`  
  - Com equipa e 1 aprovação: `./scripts/setup-branch-protection.sh main 1`

Se a API devolver **422**, os nomes dos *status checks* podem usar o prefixo do workflow: no PowerShell tente `.\scripts\setup-branch-protection.ps1 -UseWorkflowPrefix`; no Bash, terceiro argumento `ci`: `./scripts/setup-branch-protection.sh main 1 ci`. Em alternativa, abra o último run verde em **Actions**, copie os nomes exatos e ajuste o array no script (ou use a UI abaixo).

---

Configuração **manual** no repositório GitHub: **Settings → Branches → Add rule** para `main` (e `master` se existir).

## Requisitos obrigatórios

1. **Require a pull request before merging**  
   - Aprovações: pelo menos **1** (ajustar à equipa).  
   - **Dismiss stale pull request approvals when new commits are pushed**: ativado (recomendado).

2. **Require status checks to pass before merging**  
   - Marcar os jobs do workflow **`ci`**:  
     - `Spec documents` (ou nome exibido `spec-present`)  
     - `Backend (.NET)` — quando aplicável  
     - `Frontend Web (Vue)` — quando aplicável  
     - `Android (Gradle)` — quando aplicável  
   - **Nota:** quando ainda não existe `backend/**/*.sln`, só o job `spec-present` corre; ainda assim exija esse check.

3. **Require conversation resolution before merging** (opcional recomendado).

4. **Do not allow bypassing the above settings**  
   - Aplicar a **Administrators** (recomendado).

5. **Restrict who can push to matching branches** — apenas equipa de release, ou ninguém (só via PR).

6. **Block force pushes** — ativado.

## O que isto impede

- Merge com CI vermelho.  
- Push direto em `main` (se restringido).  
- Bypass acidental por administrador (se “include administrators” estiver ativo).

---

Atualizar este documento se os nomes dos jobs em `.github/workflows/ci.yml` mudarem.
