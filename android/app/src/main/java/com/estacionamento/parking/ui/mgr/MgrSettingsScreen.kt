package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import android.widget.Toast
import androidx.compose.ui.platform.LocalContext
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.RechargePackageDto
import com.estacionamento.parking.network.RechargePackageWriteBody
import com.estacionamento.parking.network.RechargePackages
import com.estacionamento.parking.network.SettingsPostBody
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

private data class PackageForm(
    val id: String = "",
    val displayName: String = "",
    val hours: String = "",
    val price: String = "",
    val isPromo: Boolean = false,
    val sortOrder: String = "0",
    val active: Boolean = true,
)

@Composable
fun MgrSettingsScreen(
    api: ParkingApi,
    role: String,
    onBack: () -> Unit,
) {
    val ctx = LocalContext.current
    val canLojInvites = role == "ADMIN" || role == "SUPER_ADMIN"
    var priceStr by remember { mutableStateOf("") }
    var capStr by remember { mutableStateOf("") }
    var clientPkgs by remember { mutableStateOf<List<RechargePackageDto>>(emptyList()) }
    var lojPkgs by remember { mutableStateOf<List<RechargePackageDto>>(emptyList()) }
    var clientPkgMsg by remember { mutableStateOf<String?>(null) }
    var lojPkgMsg by remember { mutableStateOf<String?>(null) }
    var savingScope by remember { mutableStateOf<String?>(null) }
    var clientForm by remember { mutableStateOf(PackageForm()) }
    var lojForm by remember { mutableStateOf(PackageForm()) }
    var pendingDelete by remember { mutableStateOf<Pair<String, RechargePackageDto>?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()
    val canManagePackages = role == "ADMIN" || role == "SUPER_ADMIN"

    fun packageMsg(scope: String): String? = if (scope == "CLIENT") clientPkgMsg else lojPkgMsg

    fun setPackageMsg(scope: String, value: String?) {
        if (scope == "CLIENT") clientPkgMsg = value else lojPkgMsg = value
    }

    fun packageForm(scope: String): PackageForm = if (scope == "CLIENT") clientForm else lojForm

    fun setPackageForm(scope: String, form: PackageForm) {
        if (scope == "CLIENT") clientForm = form else lojForm = form
    }

    fun setPackages(scope: String, items: List<RechargePackageDto>) {
        if (scope == "CLIENT") clientPkgs = items else lojPkgs = items
    }

    fun loadPackages(scopeName: String) {
        scope.launch {
            try {
                val items =
                    if (canManagePackages) {
                        api.rechargePackagesManage(scopeName).items
                    } else {
                        api.rechargePackages(scopeName).items
                    }
                setPackages(scopeName, items.sortedWith(RechargePackages::compare))
                setPackageMsg(scopeName, null)
            } catch (e: HttpException) {
                setPackages(scopeName, emptyList())
                setPackageMsg(scopeName, ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
            } catch (e: Exception) {
                setPackages(scopeName, emptyList())
                setPackageMsg(scopeName, e.message)
            }
        }
    }

    fun resetForm(scopeName: String) {
        setPackageForm(scopeName, PackageForm())
        setPackageMsg(scopeName, null)
    }

    fun editPackage(scopeName: String, pkg: RechargePackageDto) {
        setPackageForm(
            scopeName,
            PackageForm(
                id = pkg.id,
                displayName = pkg.displayName,
                hours = pkg.hours.toString(),
                price = pkg.price,
                isPromo = pkg.isPromo,
                sortOrder = pkg.sortOrder.toString(),
                active = pkg.active,
            ),
        )
        setPackageMsg(scopeName, null)
    }

    fun buildPayload(scopeName: String): RechargePackageWriteBody? {
        val form = packageForm(scopeName)
        val displayName = form.displayName.trim()
        val hours = form.hours.toIntOrNull()
        val price = form.price.replace(",", ".").toDoubleOrNull()
        val sortOrder = form.sortOrder.toIntOrNull()
        when {
            displayName.isBlank() -> setPackageMsg(scopeName, "Nome do pacote é obrigatório.")
            hours == null || hours < 1 -> setPackageMsg(scopeName, "Horas inválidas.")
            price == null || price < 0 -> setPackageMsg(scopeName, "Preço inválido.")
            sortOrder == null || sortOrder < 0 -> setPackageMsg(scopeName, "Ordem inválida.")
            else -> {
                setPackageMsg(scopeName, null)
                return RechargePackageWriteBody(
                    displayName = displayName,
                    scope = scopeName,
                    hours = hours,
                    price = price,
                    isPromo = form.isPromo,
                    sortOrder = sortOrder,
                    active = form.active,
                )
            }
        }
        return null
    }

    LaunchedEffect(Unit) {
        try {
            val s = api.settings()
            priceStr = s.pricePerHour
            capStr = s.capacity.toString()
        } catch (e: HttpException) {
            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
        } catch (e: Exception) {
            err = e.message
        }
        loadPackages("CLIENT")
        loadPackages("LOJISTA")
    }

    Column(Modifier.padding(16.dp)) {
        Text(UiStrings.B13, style = MaterialTheme.typography.titleLarge)
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        if (canLojInvites) {
            MgrLojistaInvitesSection(api = api)
        }
        OutlinedTextField(
            value = priceStr,
            onValueChange = { priceStr = it },
            label = { Text("Preço por hora (R\$)") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )
        OutlinedTextField(
            value = capStr,
            onValueChange = { capStr = it },
            label = { Text("Capacidade (vagas)") },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
        )
        Button(
            onClick = {
                val price = priceStr.replace(",", ".").toDoubleOrNull()
                val cap = capStr.toIntOrNull()
                if (price == null || price < 0.01) {
                    err = "Preço inválido."
                    return@Button
                }
                if (cap == null || cap < 1) {
                    err = "Capacidade inválida."
                    return@Button
                }
                scope.launch {
                    try {
                        api.settingsPost(SettingsPostBody(price, cap))
                        Toast.makeText(ctx, UiStrings.T7, Toast.LENGTH_SHORT).show()
                        err = null
                    } catch (e: HttpException) {
                        err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                    } catch (e: Exception) {
                        err = e.message
                    }
                }
            },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 12.dp)
                .semantics { contentDescription = UiStrings.Salvar },
        ) {
            Text(UiStrings.Salvar)
        }
        PackageSection(
            title = "Pacotes — CLIENTE",
            canManage = canManagePackages,
            items = clientPkgs,
            form = clientForm,
            message = packageMsg("CLIENT"),
            disabled = savingScope == "CLIENT",
            onFormChange = { clientForm = it },
            onSave = {
                val payload = buildPayload("CLIENT") ?: return@PackageSection
                scope.launch {
                    savingScope = "CLIENT"
                    try {
                        if (clientForm.id.isBlank()) {
                            api.rechargePackagesCreate(payload)
                        } else {
                            api.rechargePackagesUpdate(clientForm.id, payload)
                        }
                        Toast.makeText(ctx, UiStrings.T7, Toast.LENGTH_SHORT).show()
                        resetForm("CLIENT")
                        loadPackages("CLIENT")
                    } catch (e: HttpException) {
                        setPackageMsg("CLIENT", ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
                    } catch (e: Exception) {
                        setPackageMsg("CLIENT", e.message)
                    } finally {
                        savingScope = null
                    }
                }
            },
            onCancel = { resetForm("CLIENT") },
            onEdit = { editPackage("CLIENT", it) },
            onToggle = { pkg, active ->
                scope.launch {
                    savingScope = "CLIENT"
                    try {
                        api.rechargePackagesUpdate(
                            pkg.id,
                            RechargePackageWriteBody(
                                displayName = pkg.displayName,
                                scope = pkg.scope,
                                hours = pkg.hours,
                                price = RechargePackages.priceNumber(pkg.price),
                                isPromo = pkg.isPromo,
                                sortOrder = pkg.sortOrder,
                                active = active,
                            ),
                        )
                        loadPackages("CLIENT")
                    } catch (e: HttpException) {
                        setPackageMsg("CLIENT", ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
                    } catch (e: Exception) {
                        setPackageMsg("CLIENT", e.message)
                    } finally {
                        savingScope = null
                    }
                }
            },
            onDelete = { pkg ->
                pendingDelete = "CLIENT" to pkg
            },
        )
        PackageSection(
            title = "Pacotes — LOJISTA",
            canManage = canManagePackages,
            items = lojPkgs,
            form = lojForm,
            message = packageMsg("LOJISTA"),
            disabled = savingScope == "LOJISTA",
            onFormChange = { lojForm = it },
            onSave = {
                val payload = buildPayload("LOJISTA") ?: return@PackageSection
                scope.launch {
                    savingScope = "LOJISTA"
                    try {
                        if (lojForm.id.isBlank()) {
                            api.rechargePackagesCreate(payload)
                        } else {
                            api.rechargePackagesUpdate(lojForm.id, payload)
                        }
                        Toast.makeText(ctx, UiStrings.T7, Toast.LENGTH_SHORT).show()
                        resetForm("LOJISTA")
                        loadPackages("LOJISTA")
                    } catch (e: HttpException) {
                        setPackageMsg("LOJISTA", ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
                    } catch (e: Exception) {
                        setPackageMsg("LOJISTA", e.message)
                    } finally {
                        savingScope = null
                    }
                }
            },
            onCancel = { resetForm("LOJISTA") },
            onEdit = { editPackage("LOJISTA", it) },
            onToggle = { pkg, active ->
                scope.launch {
                    savingScope = "LOJISTA"
                    try {
                        api.rechargePackagesUpdate(
                            pkg.id,
                            RechargePackageWriteBody(
                                displayName = pkg.displayName,
                                scope = pkg.scope,
                                hours = pkg.hours,
                                price = RechargePackages.priceNumber(pkg.price),
                                isPromo = pkg.isPromo,
                                sortOrder = pkg.sortOrder,
                                active = active,
                            ),
                        )
                        loadPackages("LOJISTA")
                    } catch (e: HttpException) {
                        setPackageMsg("LOJISTA", ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
                    } catch (e: Exception) {
                        setPackageMsg("LOJISTA", e.message)
                    } finally {
                        savingScope = null
                    }
                }
            },
            onDelete = { pkg ->
                pendingDelete = "LOJISTA" to pkg
            },
        )
        Button(onClick = onBack, modifier = Modifier.padding(top = 12.dp)) { Text(UiStrings.Voltar) }
    }

    pendingDelete?.let { (scopeName, pkg) ->
        AlertDialog(
            onDismissRequest = { pendingDelete = null },
            title = { Text("Excluir pacote") },
            text = { Text("Tem certeza que deseja excluir ${RechargePackages.title(pkg)}?") },
            confirmButton = {
                Button(
                    onClick = {
                        pendingDelete = null
                        scope.launch {
                            savingScope = scopeName
                            try {
                                api.rechargePackagesDelete(pkg.id)
                                loadPackages(scopeName)
                            } catch (e: HttpException) {
                                setPackageMsg(scopeName, ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
                            } catch (e: Exception) {
                                setPackageMsg(scopeName, e.message)
                            } finally {
                                savingScope = null
                            }
                        }
                    },
                ) {
                    Text("Excluir")
                }
            },
            dismissButton = {
                Button(onClick = { pendingDelete = null }) {
                    Text(UiStrings.Voltar)
                }
            },
        )
    }
}

@Composable
private fun PackageSection(
    title: String,
    canManage: Boolean,
    items: List<RechargePackageDto>,
    form: PackageForm,
    message: String?,
    disabled: Boolean,
    onFormChange: (PackageForm) -> Unit,
    onSave: () -> Unit,
    onCancel: () -> Unit,
    onEdit: (RechargePackageDto) -> Unit,
    onToggle: (RechargePackageDto, Boolean) -> Unit,
    onDelete: (RechargePackageDto) -> Unit,
) {
    Text(title, modifier = Modifier.padding(top = 24.dp), style = MaterialTheme.typography.titleSmall)
    message?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 4.dp)) }
    if (canManage) {
        Text(
            if (form.id.isBlank()) "Novo pacote" else "Editar pacote",
            modifier = Modifier.padding(top = 12.dp),
            style = MaterialTheme.typography.titleSmall,
        )
        OutlinedTextField(
            value = form.displayName,
            onValueChange = { onFormChange(form.copy(displayName = it)) },
            label = { Text("Nome") },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        )
        OutlinedTextField(
            value = form.hours,
            onValueChange = { onFormChange(form.copy(hours = it)) },
            label = { Text("Horas") },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
        )
        OutlinedTextField(
            value = form.price,
            onValueChange = { onFormChange(form.copy(price = it)) },
            label = { Text("Preço") },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
        )
        OutlinedTextField(
            value = form.sortOrder,
            onValueChange = { onFormChange(form.copy(sortOrder = it)) },
            label = { Text("Ordem") },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
        )
        Row(Modifier.padding(top = 8.dp)) {
            Checkbox(
                checked = form.isPromo,
                onCheckedChange = { onFormChange(form.copy(isPromo = it)) },
            )
            Text("Promocional", modifier = Modifier.padding(top = 12.dp))
        }
        Row {
            Checkbox(
                checked = form.active,
                onCheckedChange = { onFormChange(form.copy(active = it)) },
            )
            Text("Ativo", modifier = Modifier.padding(top = 12.dp))
        }
        Button(onClick = onSave, enabled = !disabled, modifier = Modifier.padding(top = 8.dp)) {
            Text(if (form.id.isBlank()) "Criar pacote" else "Salvar pacote")
        }
        if (form.id.isNotBlank()) {
            Button(onClick = onCancel, enabled = !disabled, modifier = Modifier.padding(top = 8.dp)) {
                Text("Cancelar edição")
            }
        }
    }
    if (items.isEmpty()) {
        Text(UiStrings.S12, modifier = Modifier.padding(top = 8.dp))
    } else {
        LazyColumn(Modifier.padding(top = 8.dp)) {
            items(items, key = { it.id }) { pkg ->
                Column(Modifier.padding(vertical = 8.dp)) {
                    Text(
                        buildString {
                            append(RechargePackages.title(pkg))
                            if (pkg.isPromo) append(" • Promocional")
                            if (!pkg.active) append(" • Inativo")
                        },
                    )
                    Text("${pkg.hours} h — R$ ${pkg.price}")
                    Text("Ordem: ${pkg.sortOrder}")
                    if (canManage) {
                        Button(onClick = { onEdit(pkg) }, enabled = !disabled, modifier = Modifier.padding(top = 4.dp)) {
                            Text("Editar")
                        }
                        Button(
                            onClick = { onToggle(pkg, !pkg.active) },
                            enabled = !disabled,
                            modifier = Modifier.padding(top = 4.dp),
                        ) {
                            Text(if (pkg.active) "Desativar" else "Reativar")
                        }
                        Button(onClick = { onDelete(pkg) }, enabled = !disabled, modifier = Modifier.padding(top = 4.dp)) {
                            Text("Excluir")
                        }
                    }
                }
            }
        }
    }
}
