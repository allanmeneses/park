package com.estacionamento.parking.ui

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.os.Handler
import android.os.Looper
import android.widget.Toast
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.navigation.NavController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.estacionamento.parking.BuildConfig
import com.estacionamento.parking.R
import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.auth.JwtRoleParser
import com.estacionamento.parking.auth.TokenRefreshCoordinator
import com.estacionamento.parking.navigation.NavRoutes
import com.estacionamento.parking.navigation.RoleRouteAccess
import com.estacionamento.parking.network.LogoutBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.ParkingApiFactory
import com.estacionamento.parking.offline.EncryptedOfflineQueuePersistence
import com.estacionamento.parking.offline.OfflineQueueDrainer
import com.estacionamento.parking.offline.OfflineQueueStore
import com.estacionamento.parking.ui.adm.AdmTenantScreen
import com.estacionamento.parking.ui.cli.CliBuyScreen
import com.estacionamento.parking.ui.cli.CliPayCardScreen
import com.estacionamento.parking.ui.cli.CliHistoryScreen
import com.estacionamento.parking.ui.cli.CliWalletScreen
import com.estacionamento.parking.ui.common.PayPixScreen
import com.estacionamento.parking.ui.forbidden.ForbiddenScreen
import com.estacionamento.parking.ui.common.ClockSyncGate
import com.estacionamento.parking.ui.login.CliRegisterScreen
import com.estacionamento.parking.ui.login.LoginScreen
import com.estacionamento.parking.ui.login.LojRegisterScreen
import com.estacionamento.parking.ui.loj.LojBuyScreen
import com.estacionamento.parking.ui.loj.LojPayCardScreen
import com.estacionamento.parking.ui.loj.LojGrantHistoryScreen
import com.estacionamento.parking.ui.loj.LojGrantScreen
import com.estacionamento.parking.ui.loj.LojHistoryScreen
import com.estacionamento.parking.ui.loj.LojWalletScreen
import com.estacionamento.parking.ui.mgr.MgrBalancesReportScreen
import com.estacionamento.parking.ui.mgr.MgrCashScreen
import com.estacionamento.parking.ui.mgr.MgrAnalyticsScreen
import com.estacionamento.parking.ui.mgr.MgrDashboardScreen
import com.estacionamento.parking.ui.mgr.MgrMovementsScreen
import com.estacionamento.parking.ui.mgr.MgrLojistaInvitesScreen
import com.estacionamento.parking.ui.mgr.MgrPspMercadoPagoScreen
import com.estacionamento.parking.ui.mgr.MgrSettingsScreen
import com.estacionamento.parking.ui.op.OpCheckoutScreen
import com.estacionamento.parking.ui.op.OpEntryScreen
import com.estacionamento.parking.ui.op.OpHomeScreen
import com.estacionamento.parking.ui.op.OpPayCardScreen
import com.estacionamento.parking.ui.op.OpPayMethodScreen
import com.estacionamento.parking.ui.navigation.parkingComposable
import com.estacionamento.parking.ui.op.OpTicketDetailScreen
import com.estacionamento.parking.util.isNetworkConnected
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

private fun NavController.popToOpHome() {
    navigate(NavRoutes.OP_HOME) {
        popUpTo(NavRoutes.OP_HOME) { inclusive = false }
    }
}

private fun NavController.popToCliWallet() {
    navigate(NavRoutes.CLI_WALLET) {
        popUpTo(NavRoutes.CLI_WALLET) { inclusive = false }
    }
}

private fun NavController.popToLojWallet() {
    navigate(NavRoutes.LOJ_WALLET) {
        popUpTo(NavRoutes.LOJ_WALLET) { inclusive = false }
    }
}

private fun cardEmbedBaseUrl(apiBase: String): String =
    apiBase.removeSuffix("/api/v1").trimEnd('/')

@Composable
fun ParkingApp() {
    val ctx = LocalContext.current.applicationContext
    val prefs = remember { AuthPrefs(ctx) }
    var loggedIn by remember { mutableStateOf(!prefs.accessToken.isNullOrBlank()) }

    val stack = remember { ParkingApiFactory.createStack(BuildConfig.API_BASE, prefs) }
    val api = stack.api
    val offlineStore = remember { OfflineQueueStore(EncryptedOfflineQueuePersistence(ctx)) }
    val appScope = remember { CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate) }
    val coordinator = remember {
        TokenRefreshCoordinator(appScope, prefs, stack.authRefresh)
    }

    DisposableEffect(Unit) {
        onDispose {
            coordinator.cancel()
            appScope.cancel()
        }
    }

    LaunchedEffect(loggedIn) {
        if (!loggedIn) {
            coordinator.cancel()
        } else {
            coordinator.scheduleResumeFromStoredExpiry()
        }
    }

    DisposableEffect(loggedIn) {
        if (!loggedIn) {
            return@DisposableEffect onDispose { }
        }
        val drainer = OfflineQueueDrainer(offlineStore, stack.okHttpClient, stack.rootBaseUrl, prefs)
        val cm = ctx.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val mainHandler = Handler(Looper.getMainLooper())
        val callback = object : ConnectivityManager.NetworkCallback() {
            override fun onAvailable(network: Network) {
                appScope.launch {
                    drainer.drainAll(onPermanentFailure = {
                        mainHandler.post {
                            Toast.makeText(ctx, UiStrings.T9, Toast.LENGTH_LONG).show()
                        }
                    })
                }
            }
        }
        cm.registerDefaultNetworkCallback(callback)
        onDispose {
            runCatching { cm.unregisterNetworkCallback(callback) }
        }
    }

    ClockSyncGate(http = stack.okHttpClient, apiBase = BuildConfig.API_BASE) {
        if (!loggedIn) {
            val loginNav = rememberNavController()
            NavHost(
                navController = loginNav,
                startDestination = NavRoutes.LOGIN,
            ) {
                parkingComposable(NavRoutes.LOGIN) {
                    LoginScreen(
                        api = api,
                        prefs = prefs,
                        onLoggedIn = { expiresIn ->
                            coordinator.scheduleAfterLoginOrRefresh(expiresIn)
                            loggedIn = true
                        },
                        onRegisterClient = { loginNav.navigate(NavRoutes.CLI_REGISTER) },
                        onRegisterLojista = { loginNav.navigate(NavRoutes.LOJ_REGISTER) },
                    )
                }
                parkingComposable(
                    route = "${NavRoutes.CLI_REGISTER}/{parkingId}",
                    arguments = listOf(navArgument("parkingId") { type = NavType.StringType }),
                ) { entry ->
                    val raw = entry.arguments?.getString("parkingId").orEmpty()
                    CliRegisterScreen(
                        api = api,
                        prefs = prefs,
                        initialParkingId = raw.ifBlank { null },
                        onRegistered = { expiresIn ->
                            coordinator.scheduleAfterLoginOrRefresh(expiresIn)
                            loggedIn = true
                        },
                        onBack = { loginNav.popBackStack() },
                    )
                }
                parkingComposable(NavRoutes.CLI_REGISTER) {
                    CliRegisterScreen(
                        api = api,
                        prefs = prefs,
                        initialParkingId = null,
                        onRegistered = { expiresIn ->
                            coordinator.scheduleAfterLoginOrRefresh(expiresIn)
                            loggedIn = true
                        },
                        onBack = { loginNav.popBackStack() },
                    )
                }
                parkingComposable(NavRoutes.LOJ_REGISTER) {
                    LojRegisterScreen(
                        api = api,
                        prefs = prefs,
                        onRegistered = { expiresIn ->
                            coordinator.scheduleAfterLoginOrRefresh(expiresIn)
                            loggedIn = true
                        },
                        onBack = { loginNav.popBackStack() },
                    )
                }
            }
        } else {
            val token = prefs.accessToken
            if (token.isNullOrBlank()) {
                loggedIn = false
            } else {
                val role = JwtRoleParser.roleFromAccessToken(token)
                if (role == null) {
                    prefs.clear()
                    loggedIn = false
                } else {
                    key(prefs.activeParkingId, role) {
                        AuthenticatedNavHost(
                            role = role,
                            prefs = prefs,
                            api = api,
                            apiRootUrl = cardEmbedBaseUrl(stack.rootBaseUrl),
                            apiV1BaseUrl = stack.rootBaseUrl,
                            offlineStore = offlineStore,
                            isOnline = { ctx.isNetworkConnected() },
                            onLogout = {
                                appScope.launch {
                                    try {
                                        prefs.refreshToken?.let { api.logout(LogoutBody(it)) }
                                    } catch (_: Exception) {
                                        // best effort; local cleanup remains mandatory
                                    } finally {
                                        coordinator.cancel()
                                        prefs.clear()
                                        loggedIn = false
                                    }
                                }
                            },
                        )
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AuthenticatedNavHost(
    role: String,
    prefs: AuthPrefs,
    api: ParkingApi,
    apiRootUrl: String,
    apiV1BaseUrl: String,
    offlineStore: OfflineQueueStore,
    isOnline: () -> Boolean,
    onLogout: () -> Unit,
) {
    val nav = rememberNavController()
    val ctx = LocalContext.current
    val superHasParking = role != "SUPER_ADMIN" || !prefs.activeParkingId.isNullOrBlank()
    val start = RoleRouteAccess.startDestination(role, superHasParking)
    val showDualTabs = RoleRouteAccess.showsOperacaoGestaoTabs(role) && superHasParking
    val navBackStackEntry by nav.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route.orEmpty()

    LaunchedEffect(currentRoute, role, superHasParking) {
        if (currentRoute.isBlank()) return@LaunchedEffect
        val normalized = RoleRouteAccess.normalizeRoute(currentRoute)
        if (normalized == NavRoutes.FORBIDDEN) return@LaunchedEffect
        if (!RoleRouteAccess.canAccess(role, normalized, superHasParking)) {
            nav.navigate(NavRoutes.FORBIDDEN) {
                launchSingleTop = true
            }
        }
    }

    Scaffold(
        bottomBar = {
            if (showDualTabs) {
                val route = currentRoute
                val onOp = route == NavRoutes.OP_HOME ||
                    route == NavRoutes.OP_ENTRY_PLATE ||
                    route.startsWith("${NavRoutes.OP_TICKET_DETAIL}/") ||
                    route.startsWith("${NavRoutes.OP_CHECKOUT}/") ||
                    route.startsWith("${NavRoutes.OP_PAY_METHOD}/") ||
                    route.startsWith("${NavRoutes.OP_PAY_PIX}/") ||
                    route.startsWith("${NavRoutes.OP_PAY_CARD}/")
                val onMgr = route in NavRoutes.adminManagementRoutes
                NavigationBar(
                    containerColor = MaterialTheme.colorScheme.surfaceVariant,
                    tonalElevation = 3.dp,
                ) {
                    NavigationBarItem(
                        selected = onOp,
                        onClick = { nav.navigate(NavRoutes.OP_HOME) { launchSingleTop = true } },
                        icon = {
                            Icon(
                                painter = painterResource(R.drawable.ic_nav_operacao),
                                contentDescription = null,
                            )
                        },
                        label = { Text(UiStrings.B21) },
                        modifier = Modifier.semantics { contentDescription = UiStrings.B21 },
                        colors =
                            NavigationBarItemDefaults.colors(
                                selectedIconColor = MaterialTheme.colorScheme.primary,
                                selectedTextColor = MaterialTheme.colorScheme.primary,
                                indicatorColor = MaterialTheme.colorScheme.primaryContainer,
                            ),
                    )
                    NavigationBarItem(
                        selected = onMgr,
                        onClick = { nav.navigate(NavRoutes.MGR_DASHBOARD) { launchSingleTop = true } },
                        icon = {
                            Icon(
                                painter = painterResource(R.drawable.ic_nav_gestao),
                                contentDescription = null,
                            )
                        },
                        label = { Text(UiStrings.B20) },
                        modifier = Modifier.semantics { contentDescription = UiStrings.B20 },
                        colors =
                            NavigationBarItemDefaults.colors(
                                selectedIconColor = MaterialTheme.colorScheme.primary,
                                selectedTextColor = MaterialTheme.colorScheme.primary,
                                indicatorColor = MaterialTheme.colorScheme.primaryContainer,
                            ),
                    )
                }
            }
        },
    ) { padding ->
        NavHost(
            navController = nav,
            startDestination = start,
            modifier = Modifier.padding(padding),
        ) {
            parkingComposable(NavRoutes.ADM_TENANT) {
                AdmTenantScreen(
                    api = api,
                    prefs = prefs,
                    onGestao = { nav.navigate(NavRoutes.MGR_DASHBOARD) },
                    onOperacao = { nav.navigate(NavRoutes.OP_HOME) },
                    onLogout = onLogout,
                )
            }
            parkingComposable(NavRoutes.OP_HOME) {
                OpHomeScreen(
                    api = api,
                    onNewEntry = { nav.navigate(NavRoutes.OP_ENTRY_PLATE) },
                    onTicket = { id -> nav.navigate("${NavRoutes.OP_TICKET_DETAIL}/$id") },
                    onLogout = onLogout,
                )
            }
            parkingComposable(NavRoutes.OP_ENTRY_PLATE) {
                OpEntryScreen(
                    api = api,
                    offlineStore = offlineStore,
                    isOnline = isOnline,
                    onDone = {
                        nav.popBackStack(NavRoutes.OP_HOME, inclusive = false)
                    },
                    onBack = { nav.popBackStack() },
                    onQueued = {
                        nav.popBackStack(NavRoutes.OP_HOME, inclusive = false)
                    },
                )
            }
            parkingComposable(
                route = "${NavRoutes.OP_TICKET_DETAIL}/{id}",
                arguments = listOf(navArgument("id") { type = NavType.StringType }),
            ) { entry ->
                val id = entry.arguments?.getString("id").orEmpty()
                OpTicketDetailScreen(
                    api = api,
                    ticketId = id,
                    onBack = { nav.popBackStack() },
                    onCheckout = { tid -> nav.navigate("${NavRoutes.OP_CHECKOUT}/$tid") },
                    onPay = { payId -> nav.navigate("${NavRoutes.OP_PAY_METHOD}/$payId") },
                )
            }
            parkingComposable(
                route = "${NavRoutes.OP_CHECKOUT}/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.StringType }),
            ) { entry ->
                val ticketId = entry.arguments?.getString("ticketId").orEmpty()
                OpCheckoutScreen(
                    api = api,
                    offlineStore = offlineStore,
                    isOnline = isOnline,
                    ticketId = ticketId,
                    onZeroAmount = {
                        nav.popToOpHome()
                    },
                    onNeedPayment = { payId ->
                        nav.navigate("${NavRoutes.OP_PAY_METHOD}/$payId") {
                            popUpTo("${NavRoutes.OP_CHECKOUT}/$ticketId") { inclusive = true }
                        }
                    },
                    onInvalidState = {
                        Toast.makeText(ctx, UiStrings.E6, Toast.LENGTH_LONG).show()
                        nav.popBackStack()
                    },
                    onBack = { nav.popBackStack() },
                    onCheckoutQueued = { nav.popToOpHome() },
                )
            }
            parkingComposable(
                route = "${NavRoutes.OP_PAY_METHOD}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                OpPayMethodScreen(
                    api = api,
                    paymentId = paymentId,
                    onPix = { nav.navigate("${NavRoutes.OP_PAY_PIX}/$paymentId") },
                    onCard = { nav.navigate("${NavRoutes.OP_PAY_CARD}/$paymentId") },
                    onCashSuccess = {
                        Toast.makeText(ctx, UiStrings.T4, Toast.LENGTH_SHORT).show()
                        nav.popToOpHome()
                    },
                    onNothingToPay = {
                        Toast.makeText(ctx, UiStrings.T3, Toast.LENGTH_LONG).show()
                        nav.popToOpHome()
                    },
                    onBack = { nav.popBackStack() },
                )
            }
            parkingComposable(
                route = "${NavRoutes.OP_PAY_PIX}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                PayPixScreen(
                    api = api,
                    paymentId = paymentId,
                    paidToast = UiStrings.T4,
                    onPaid = { nav.popToOpHome() },
                    onBack = { nav.popBackStack() },
                    onFailedBack = { nav.popBackStack() },
                )
            }
            parkingComposable(
                route = "${NavRoutes.OP_PAY_CARD}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                OpPayCardScreen(
                    api = api,
                    paymentId = paymentId,
                    preferSandboxCheckoutUrl = BuildConfig.DEBUG,
                    onSuccess = {
                        Toast.makeText(ctx, UiStrings.T4, Toast.LENGTH_SHORT).show()
                        nav.popToOpHome()
                    },
                    onAmountMismatch = {
                        Toast.makeText(ctx, UiStrings.E8, Toast.LENGTH_LONG).show()
                    },
                    onBack = { nav.popBackStack() },
                )
            }
            parkingComposable(NavRoutes.MGR_DASHBOARD) {
                val onLoj =
                    if (role == "ADMIN" || role == "SUPER_ADMIN") {
                        { nav.navigate(NavRoutes.MGR_LOJISTA_INVITES) }
                    } else {
                        null
                    }
                MgrDashboardScreen(
                    api = api,
                    onInsights = { nav.navigate(NavRoutes.MGR_MOVEMENTS) },
                    onAnalytics = { nav.navigate(NavRoutes.MGR_ANALYTICS) },
                    onBalancesReport = { nav.navigate(NavRoutes.MGR_BALANCES_REPORT) },
                    onCash = { nav.navigate(NavRoutes.MGR_CASH) },
                    onLojistaCadastro = onLoj,
                    onSettings = { nav.navigate(NavRoutes.MGR_SETTINGS) },
                    onOperacao = { nav.navigate(NavRoutes.OP_HOME) },
                    onLogout = onLogout,
                )
            }
            parkingComposable(NavRoutes.MGR_MOVEMENTS) {
                MgrMovementsScreen(
                    api = api,
                    onBack = { nav.popBackStack() },
                    onAnalytics = { nav.navigate(NavRoutes.MGR_ANALYTICS) },
                )
            }
            parkingComposable(NavRoutes.MGR_ANALYTICS) {
                MgrAnalyticsScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.MGR_BALANCES_REPORT) {
                MgrBalancesReportScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.MGR_CASH) {
                MgrCashScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.MGR_LOJISTA_INVITES) {
                MgrLojistaInvitesScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.MGR_SETTINGS) {
                MgrSettingsScreen(
                    api = api,
                    prefs = prefs,
                    role = role,
                    onBack = { nav.popBackStack() },
                    onPspMercadoPago = { nav.navigate(NavRoutes.MGR_PSP_MERCADOPAGO) },
                )
            }
            parkingComposable(NavRoutes.MGR_PSP_MERCADOPAGO) {
                MgrPspMercadoPagoScreen(
                    api = api,
                    prefs = prefs,
                    role = role,
                    apiV1BaseUrl = apiV1BaseUrl,
                    onBack = { nav.popBackStack() },
                )
            }
            parkingComposable(NavRoutes.CLI_WALLET) {
                CliWalletScreen(
                    api = api,
                    onHistory = { nav.navigate(NavRoutes.CLI_HISTORY) },
                    onBuy = { nav.navigate(NavRoutes.CLI_BUY) },
                    onLogout = onLogout,
                )
            }
            parkingComposable(NavRoutes.CLI_HISTORY) {
                CliHistoryScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.CLI_BUY) {
                CliBuyScreen(
                    api = api,
                    onBack = { nav.popBackStack() },
                    onPayPix = { pid -> nav.navigate("${NavRoutes.CLI_PAY_PIX}/$pid") },
                    onPayCard = { pid -> nav.navigate("${NavRoutes.CLI_PAY_CARD}/$pid") },
                )
            }
            parkingComposable(
                route = "${NavRoutes.CLI_PAY_PIX}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                PayPixScreen(
                    api = api,
                    paymentId = paymentId,
                    paidToast = UiStrings.T8,
                    onPaid = { nav.popToCliWallet() },
                    onBack = { nav.popBackStack() },
                    onFailedBack = { nav.popBackStack() },
                )
            }
            parkingComposable(
                route = "${NavRoutes.CLI_PAY_CARD}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                CliPayCardScreen(
                    api = api,
                    paymentId = paymentId,
                    apiRootUrl = apiRootUrl,
                    accessToken = prefs.accessToken.orEmpty(),
                    onPaid = { nav.popToCliWallet() },
                    onBack = { nav.popBackStack() },
                )
            }
            parkingComposable(NavRoutes.LOJ_WALLET) {
                LojWalletScreen(
                    api = api,
                    onHistory = { nav.navigate(NavRoutes.LOJ_HISTORY) },
                    onBuy = { nav.navigate(NavRoutes.LOJ_BUY) },
                    onGrant = { nav.navigate(NavRoutes.LOJ_GRANT) },
                    onGrantHistory = { nav.navigate(NavRoutes.LOJ_GRANT_HISTORY) },
                    onLogout = onLogout,
                )
            }
            parkingComposable(NavRoutes.LOJ_GRANT) {
                LojGrantScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.LOJ_GRANT_HISTORY) {
                LojGrantHistoryScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.LOJ_HISTORY) {
                LojHistoryScreen(api = api, onBack = { nav.popBackStack() })
            }
            parkingComposable(NavRoutes.LOJ_BUY) {
                LojBuyScreen(
                    api = api,
                    onBack = { nav.popBackStack() },
                    onPayPix = { pid -> nav.navigate("${NavRoutes.LOJ_PAY_PIX}/$pid") },
                    onPayCard = { pid -> nav.navigate("${NavRoutes.LOJ_PAY_CARD}/$pid") },
                )
            }
            parkingComposable(
                route = "${NavRoutes.LOJ_PAY_PIX}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                PayPixScreen(
                    api = api,
                    paymentId = paymentId,
                    paidToast = UiStrings.T8,
                    onPaid = { nav.popToLojWallet() },
                    onBack = { nav.popBackStack() },
                    onFailedBack = { nav.popBackStack() },
                )
            }
            parkingComposable(
                route = "${NavRoutes.LOJ_PAY_CARD}/{paymentId}",
                arguments = listOf(navArgument("paymentId") { type = NavType.StringType }),
            ) { entry ->
                val paymentId = entry.arguments?.getString("paymentId").orEmpty()
                LojPayCardScreen(
                    api = api,
                    paymentId = paymentId,
                    apiRootUrl = apiRootUrl,
                    accessToken = prefs.accessToken.orEmpty(),
                    onPaid = { nav.popToLojWallet() },
                    onBack = { nav.popBackStack() },
                )
            }
            parkingComposable(NavRoutes.FORBIDDEN) {
                ForbiddenScreen(
                    onGoHome = {
                        nav.navigate(start) {
                            popUpTo(start) { inclusive = true }
                            launchSingleTop = true
                        }
                    },
                )
            }
        }
    }
}
