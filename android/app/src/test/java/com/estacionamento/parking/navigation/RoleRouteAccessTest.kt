package com.estacionamento.parking.navigation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RoleRouteAccessTest {
    @Test
    fun operator_only_operation_routes() {
        assertTrue(RoleRouteAccess.canAccess("OPERATOR", NavRoutes.OP_HOME))
        assertTrue(RoleRouteAccess.canAccess("OPERATOR", NavRoutes.OP_ENTRY_PLATE))
        assertFalse(RoleRouteAccess.canAccess("OPERATOR", NavRoutes.MGR_DASHBOARD))
    }

    @Test
    fun manager_operation_and_management() {
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.OP_HOME))
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_DASHBOARD))
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_MOVEMENTS))
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_ANALYTICS))
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_BALANCES_REPORT))
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_CASH))
        assertTrue(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_PSP_MERCADOPAGO))
        assertFalse(RoleRouteAccess.canAccess("MANAGER", NavRoutes.CLI_WALLET))
        assertFalse(RoleRouteAccess.canAccess("MANAGER", NavRoutes.MGR_LOJISTA_INVITES))
    }

    @Test
    fun admin_tenant_only_super_not_ADMIN() {
        assertFalse(RoleRouteAccess.canAccess("ADMIN", NavRoutes.ADM_TENANT))
        assertTrue(RoleRouteAccess.canAccess("ADMIN", NavRoutes.MGR_DASHBOARD))
        assertTrue(RoleRouteAccess.canAccess("ADMIN", NavRoutes.MGR_MOVEMENTS))
        assertTrue(RoleRouteAccess.canAccess("ADMIN", NavRoutes.MGR_ANALYTICS))
        assertTrue(RoleRouteAccess.canAccess("ADMIN", NavRoutes.MGR_LOJISTA_INVITES))
    }

    @Test
    fun client_only_cli() {
        assertTrue(RoleRouteAccess.canAccess("CLIENT", NavRoutes.CLI_WALLET))
        assertTrue(RoleRouteAccess.canAccess("CLIENT", NavRoutes.CLI_HISTORY))
        assertTrue(RoleRouteAccess.canAccess("CLIENT", NavRoutes.CLI_PAY_CARD))
        assertFalse(RoleRouteAccess.canAccess("CLIENT", NavRoutes.OP_HOME))
    }

    @Test
    fun lojista_only_loj() {
        assertTrue(RoleRouteAccess.canAccess("LOJISTA", NavRoutes.LOJ_WALLET))
        assertTrue(RoleRouteAccess.canAccess("LOJISTA", NavRoutes.LOJ_PAY_CARD))
        assertTrue(RoleRouteAccess.canAccess("LOJISTA", NavRoutes.LOJ_GRANT))
        assertTrue(RoleRouteAccess.canAccess("LOJISTA", NavRoutes.LOJ_GRANT_HISTORY))
        assertFalse(RoleRouteAccess.canAccess("LOJISTA", NavRoutes.CLI_WALLET))
    }

    @Test
    fun super_admin_without_parking_only_adm_and_forbidden() {
        assertTrue(RoleRouteAccess.canAccess("SUPER_ADMIN", NavRoutes.ADM_TENANT, superAdminHasParking = false))
        assertFalse(RoleRouteAccess.canAccess("SUPER_ADMIN", NavRoutes.OP_HOME, superAdminHasParking = false))
    }

    @Test
    fun super_admin_with_parking_like_manager() {
        assertTrue(RoleRouteAccess.canAccess("SUPER_ADMIN", NavRoutes.OP_HOME, superAdminHasParking = true))
        assertTrue(RoleRouteAccess.canAccess("SUPER_ADMIN", NavRoutes.MGR_DASHBOARD, superAdminHasParking = true))
        assertTrue(RoleRouteAccess.canAccess("SUPER_ADMIN", NavRoutes.MGR_BALANCES_REPORT, superAdminHasParking = true))
        assertTrue(RoleRouteAccess.canAccess("SUPER_ADMIN", NavRoutes.MGR_LOJISTA_INVITES, superAdminHasParking = true))
    }

    @Test
    fun startDestination_matches_spec_defaults() {
        assertEquals(NavRoutes.OP_HOME, RoleRouteAccess.startDestination("OPERATOR", false))
        assertEquals(NavRoutes.MGR_DASHBOARD, RoleRouteAccess.startDestination("MANAGER", false))
        assertEquals(NavRoutes.MGR_DASHBOARD, RoleRouteAccess.startDestination("ADMIN", false))
        assertEquals(NavRoutes.CLI_WALLET, RoleRouteAccess.startDestination("CLIENT", false))
        assertEquals(NavRoutes.LOJ_WALLET, RoleRouteAccess.startDestination("LOJISTA", false))
        assertEquals(NavRoutes.ADM_TENANT, RoleRouteAccess.startDestination("SUPER_ADMIN", false))
        assertEquals(NavRoutes.MGR_DASHBOARD, RoleRouteAccess.startDestination("SUPER_ADMIN", true))
    }

    @Test
    fun showsManagementTabs_like_spec_section_6() {
        assertTrue(RoleRouteAccess.showsOperacaoGestaoTabs("MANAGER"))
        assertTrue(RoleRouteAccess.showsOperacaoGestaoTabs("ADMIN"))
        assertTrue(RoleRouteAccess.showsOperacaoGestaoTabs("SUPER_ADMIN"))
        assertFalse(RoleRouteAccess.showsOperacaoGestaoTabs("OPERATOR"))
        assertFalse(RoleRouteAccess.showsOperacaoGestaoTabs("CLIENT"))
    }

    @Test
    fun dynamic_routes_are_normalized_before_access_check() {
        assertTrue(RoleRouteAccess.canAccess("OPERATOR", "op_ticket_detail/123"))
        assertTrue(RoleRouteAccess.canAccess("CLIENT", "cli_pay_pix/pay-1"))
        assertTrue(RoleRouteAccess.canAccess("CLIENT", "cli_pay_card/pay-1"))
        assertTrue(RoleRouteAccess.canAccess("LOJISTA", "loj_pay_card/pay-2"))
        assertFalse(RoleRouteAccess.canAccess("CLIENT", "op_checkout/ticket-1"))
    }
}
