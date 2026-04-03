const allowed: Record<string, readonly string[]> = {
  OPERATOR: [
    'login',
    'op_home',
    'op_entry_plate',
    'op_ticket_detail',
    'op_checkout',
    'op_pay_method',
    'op_pay_pix',
    'op_pay_card',
    'forbidden',
  ],
  MANAGER: [
    'login',
    'op_home',
    'op_entry_plate',
    'op_ticket_detail',
    'op_checkout',
    'op_pay_method',
    'op_pay_pix',
    'op_pay_card',
    'mgr_dashboard',
    'mgr_movements',
    'mgr_analytics',
    'mgr_cash',
    'mgr_settings',
    'forbidden',
  ],
  ADMIN: [
    'login',
    'op_home',
    'op_entry_plate',
    'op_ticket_detail',
    'op_checkout',
    'op_pay_method',
    'op_pay_pix',
    'op_pay_card',
    'mgr_dashboard',
    'mgr_movements',
    'mgr_analytics',
    'mgr_cash',
    'mgr_lojista_invites',
    'mgr_settings',
    'forbidden',
  ],
  CLIENT: ['login', 'cli_wallet', 'cli_history', 'cli_buy', 'cli_pay_pix', 'forbidden'],
  LOJISTA: [
    'login',
    'loj_wallet',
    'loj_history',
    'loj_buy',
    'loj_pay_pix',
    'loj_grant',
    'loj_grant_history',
    'forbidden',
  ],
  SUPER_ADMIN: [
    'login',
    'adm_tenant',
    'op_home',
    'op_entry_plate',
    'op_ticket_detail',
    'op_checkout',
    'op_pay_method',
    'op_pay_pix',
    'op_pay_card',
    'mgr_dashboard',
    'mgr_movements',
    'mgr_analytics',
    'mgr_cash',
    'mgr_lojista_invites',
    'mgr_settings',
    'forbidden',
  ],
}

/** SPEC_FRONTEND §6 — matriz rota × role (exceto login sempre permitido no guard). */
export function isRouteAllowedForRole(
  role: string | null,
  name: string | symbol | undefined,
): boolean {
  if (name == null || typeof name !== 'string') return true
  /** Público: cadastro lojista (§ convites) — guard principal redireciona se já autenticado. */
  if (name === 'login' || name === 'loj_register') return true
  const list = role ? allowed[role] : undefined
  if (!list) return false
  return list.includes(name)
}
