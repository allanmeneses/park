package com.estacionamento.parking.navigation

import com.estacionamento.parking.navigation.NavRoutes.ADM_TENANT
import com.estacionamento.parking.navigation.NavRoutes.FORBIDDEN
import com.estacionamento.parking.navigation.NavRoutes.clientRoutes
import com.estacionamento.parking.navigation.NavRoutes.lojistaRoutes
import com.estacionamento.parking.navigation.NavRoutes.adminManagementRoutes
import com.estacionamento.parking.navigation.NavRoutes.managerManagementRoutes
import com.estacionamento.parking.navigation.NavRoutes.operationRoutes

/** SPEC_FRONTEND §6 — matriz rota × role. */
object RoleRouteAccess {
    private val operatorAllowed = operationRoutes + FORBIDDEN
    private val managerAllowed = operationRoutes + managerManagementRoutes + FORBIDDEN
    private val adminAllowed = operationRoutes + adminManagementRoutes + FORBIDDEN
    private val clientAllowed = clientRoutes + FORBIDDEN
    private val lojistaAllowed = lojistaRoutes + FORBIDDEN
    private val superWithParking = adminAllowed + ADM_TENANT
    private val superNoParking = setOf(ADM_TENANT, FORBIDDEN)

    fun canAccess(role: String, route: String, superAdminHasParking: Boolean = true): Boolean {
        val normalized = normalizeRoute(route)
        if (role == "SUPER_ADMIN") {
            return if (superAdminHasParking) normalized in superWithParking else normalized in superNoParking
        }
        return when (role) {
            "OPERATOR" -> normalized in operatorAllowed
            "MANAGER" -> normalized in managerAllowed
            "ADMIN" -> normalized in adminAllowed
            "CLIENT" -> normalized in clientAllowed
            "LOJISTA" -> normalized in lojistaAllowed
            else -> false
        }
    }

    fun normalizeRoute(route: String): String {
        if (route.startsWith("${NavRoutes.OP_TICKET_DETAIL}/")) return NavRoutes.OP_TICKET_DETAIL
        if (route.startsWith("${NavRoutes.OP_CHECKOUT}/")) return NavRoutes.OP_CHECKOUT
        if (route.startsWith("${NavRoutes.OP_PAY_METHOD}/")) return NavRoutes.OP_PAY_METHOD
        if (route.startsWith("${NavRoutes.OP_PAY_PIX}/")) return NavRoutes.OP_PAY_PIX
        if (route.startsWith("${NavRoutes.OP_PAY_CARD}/")) return NavRoutes.OP_PAY_CARD
        if (route.startsWith("${NavRoutes.CLI_PAY_PIX}/")) return NavRoutes.CLI_PAY_PIX
        if (route.startsWith("${NavRoutes.LOJ_PAY_PIX}/")) return NavRoutes.LOJ_PAY_PIX
        return route
    }

    fun startDestination(role: String, superAdminHasParking: Boolean): String = when (role) {
        "OPERATOR" -> NavRoutes.OP_HOME
        "MANAGER", "ADMIN" -> NavRoutes.MGR_DASHBOARD
        "CLIENT" -> NavRoutes.CLI_WALLET
        "LOJISTA" -> NavRoutes.LOJ_WALLET
        "SUPER_ADMIN" ->
            if (superAdminHasParking) NavRoutes.MGR_DASHBOARD else NavRoutes.ADM_TENANT
        else -> NavRoutes.LOGIN
    }

    fun showsOperacaoGestaoTabs(role: String): Boolean =
        role == "MANAGER" || role == "ADMIN" || role == "SUPER_ADMIN"
}
