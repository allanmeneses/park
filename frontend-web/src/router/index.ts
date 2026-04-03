import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { getActiveParkingId } from '@/session/activeParking'
import { isRouteAllowedForRole } from '@/router/roleAccess'

function superNeedsParking(): boolean {
  const auth = useAuthStore()
  return auth.role === 'SUPER_ADMIN' && !getActiveParkingId()
}

const publicRouteNames = new Set(['login', 'loj_register'])

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue') },
    {
      path: '/cadastro/lojista',
      name: 'loj_register',
      component: () => import('@/views/LojRegisterView.vue'),
    },
    { path: '/operador', name: 'op_home', component: () => import('@/views/op/OpHomeView.vue') },
    {
      path: '/operador/ticket/:id',
      name: 'op_ticket_detail',
      component: () => import('@/views/op/OpTicketDetailView.vue'),
      props: true,
    },
    {
      path: '/operador/entrada',
      name: 'op_entry_plate',
      component: () => import('@/views/op/OpEntryPlateView.vue'),
    },
    {
      path: '/operador/checkout/:ticketId',
      name: 'op_checkout',
      component: () => import('@/views/op/OpCheckoutView.vue'),
      props: true,
    },
    {
      path: '/operador/pagar/:paymentId',
      name: 'op_pay_method',
      component: () => import('@/views/op/OpPayMethodView.vue'),
      props: true,
    },
    {
      path: '/operador/pix/:paymentId',
      name: 'op_pay_pix',
      component: () => import('@/views/op/OpPayPixView.vue'),
      props: true,
    },
    {
      path: '/operador/cartao/:paymentId',
      name: 'op_pay_card',
      component: () => import('@/views/op/OpPayCardView.vue'),
      props: true,
    },
    { path: '/gestor', name: 'mgr_dashboard', component: () => import('@/views/mgr/MgrDashboardView.vue') },
    { path: '/gestor/movimentos', name: 'mgr_movements', component: () => import('@/views/mgr/MgrMovementsView.vue') },
    { path: '/gestor/analises', name: 'mgr_analytics', component: () => import('@/views/mgr/MgrAnalyticsView.vue') },
    { path: '/gestor/caixa', name: 'mgr_cash', component: () => import('@/views/mgr/MgrCashView.vue') },
    {
      path: '/gestor/lojista-convites',
      name: 'mgr_lojista_invites',
      component: () => import('@/views/mgr/MgrLojistaInvitesView.vue'),
    },
    { path: '/gestor/config', name: 'mgr_settings', component: () => import('@/views/mgr/MgrSettingsView.vue') },
    { path: '/cliente', name: 'cli_wallet', component: () => import('@/views/cli/CliWalletView.vue') },
    {
      path: '/cliente/historico',
      name: 'cli_history',
      component: () => import('@/views/cli/CliHistoryView.vue'),
    },
    { path: '/cliente/comprar', name: 'cli_buy', component: () => import('@/views/cli/CliBuyView.vue') },
    {
      path: '/cliente/pix/:paymentId',
      name: 'cli_pay_pix',
      component: () => import('@/views/cli/CliPayPixView.vue'),
      props: true,
    },
    { path: '/lojista', name: 'loj_wallet', component: () => import('@/views/loj/LojWalletView.vue') },
    {
      path: '/lojista/historico',
      name: 'loj_history',
      component: () => import('@/views/loj/LojHistoryView.vue'),
    },
    { path: '/lojista/comprar', name: 'loj_buy', component: () => import('@/views/loj/LojBuyView.vue') },
    { path: '/lojista/bonificar', name: 'loj_grant', component: () => import('@/views/loj/LojGrantView.vue') },
    {
      path: '/lojista/bonificacoes',
      name: 'loj_grant_history',
      component: () => import('@/views/loj/LojGrantHistoryView.vue'),
    },
    {
      path: '/lojista/pix/:paymentId',
      name: 'loj_pay_pix',
      component: () => import('@/views/loj/LojPayPixView.vue'),
      props: true,
    },
    { path: '/admin/tenant', name: 'adm_tenant', component: () => import('@/views/AdmTenantView.vue') },
    { path: '/proibido', name: 'forbidden', component: () => import('@/views/ForbiddenView.vue') },
    { path: '/:pathMatch(.*)*', redirect: '/login' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  const hasAccess = !!auth.accessToken || !!sessionStorage.getItem('parking.v1.access')

  if (to.name != null && publicRouteNames.has(String(to.name))) {
    if (hasAccess) {
      return defaultHome()
    }
    return true
  }

  if (!hasAccess) return { path: '/login' }

  auth.loadFromStorage()
  if (!isRouteAllowedForRole(auth.role, to.name as string)) {
    return { path: '/proibido' }
  }

  if (
    auth.role === 'SUPER_ADMIN' &&
    to.name !== 'adm_tenant' &&
    to.name !== 'login' &&
    to.name !== 'loj_register' &&
    to.name !== 'forbidden' &&
    superNeedsParking()
  ) {
    return { path: '/admin/tenant' }
  }

  return true
})

function defaultHome(): { path: string } {
  const auth = useAuthStore()
  auth.loadFromStorage()
  const r = auth.role
  if (r === 'OPERATOR') return { path: '/operador' }
  if (r === 'MANAGER' || r === 'ADMIN') return { path: '/gestor' }
  if (r === 'CLIENT') return { path: '/cliente' }
  if (r === 'LOJISTA') return { path: '/lojista' }
  if (r === 'SUPER_ADMIN') return { path: '/admin/tenant' }
  return { path: '/login' }
}
