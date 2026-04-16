package com.estacionamento.parking.ui.navigation

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.runtime.Composable
import androidx.navigation.NamedNavArgument
import androidx.navigation.NavBackStackEntry
import androidx.navigation.NavDeepLink
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable

private const val T_MS = 280

/** Transições discretas (avanço / retrocesso) para evitar saltos bruscos entre ecrãs. */
fun NavGraphBuilder.parkingComposable(
    route: String,
    arguments: List<NamedNavArgument> = emptyList(),
    deepLinks: List<NavDeepLink> = emptyList(),
    content: @Composable (NavBackStackEntry) -> Unit,
) {
    composable(
        route = route,
        arguments = arguments,
        deepLinks = deepLinks,
        enterTransition = {
            fadeIn(animationSpec = tween(T_MS, easing = FastOutSlowInEasing)) +
                slideInHorizontally(animationSpec = tween(T_MS, easing = FastOutSlowInEasing)) { full ->
                    (full * 0.12f).toInt()
                }
        },
        exitTransition = {
            fadeOut(animationSpec = tween(180))
        },
        popEnterTransition = {
            fadeIn(animationSpec = tween(T_MS, easing = FastOutSlowInEasing)) +
                slideInHorizontally(animationSpec = tween(T_MS, easing = FastOutSlowInEasing)) { full ->
                    -(full * 0.12f).toInt()
                }
        },
        popExitTransition = {
            fadeOut(animationSpec = tween(180)) +
                slideOutHorizontally(animationSpec = tween(T_MS, easing = FastOutSlowInEasing)) { full ->
                    (full * 0.12f).toInt()
                }
        },
    ) { entry ->
        content(entry)
    }
}
