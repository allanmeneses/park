package com.estacionamento.parking.navigation

/** IDs alinhados a SPEC_FRONTEND §4.4 / §6 (NavHost). */
object NavRoutes {
    const val LOGIN = "login"
    const val OP_HOME = "op_home"
    const val OP_ENTRY_PLATE = "op_entry_plate"
    const val OP_TICKET_DETAIL = "op_ticket_detail"
    const val OP_CHECKOUT = "op_checkout"
    const val OP_PAY_METHOD = "op_pay_method"
    const val OP_PAY_PIX = "op_pay_pix"
    const val OP_PAY_CARD = "op_pay_card"
    const val MGR_DASHBOARD = "mgr_dashboard"
    const val MGR_MOVEMENTS = "mgr_movements"
    const val MGR_ANALYTICS = "mgr_analytics"
    const val MGR_CASH = "mgr_cash"
    const val MGR_SETTINGS = "mgr_settings"
    const val CLI_WALLET = "cli_wallet"
    const val CLI_HISTORY = "cli_history"
    const val CLI_BUY = "cli_buy"
    const val CLI_PAY_PIX = "cli_pay_pix"
    const val LOJ_WALLET = "loj_wallet"
    const val LOJ_HISTORY = "loj_history"
    const val LOJ_BUY = "loj_buy"
    const val LOJ_PAY_PIX = "loj_pay_pix"
    const val ADM_TENANT = "adm_tenant"
    const val FORBIDDEN = "forbidden"

    val operationRoutes = setOf(
        OP_HOME, OP_ENTRY_PLATE, OP_TICKET_DETAIL, OP_CHECKOUT,
        OP_PAY_METHOD, OP_PAY_PIX, OP_PAY_CARD,
    )
    val managementRoutes = setOf(MGR_DASHBOARD, MGR_MOVEMENTS, MGR_ANALYTICS, MGR_CASH, MGR_SETTINGS)
    val clientRoutes = setOf(CLI_WALLET, CLI_HISTORY, CLI_BUY, CLI_PAY_PIX)
    val lojistaRoutes = setOf(LOJ_WALLET, LOJ_HISTORY, LOJ_BUY, LOJ_PAY_PIX)
}
