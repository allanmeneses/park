# Branch protection (GitHub) — SPEC v8.7 §25.5

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
