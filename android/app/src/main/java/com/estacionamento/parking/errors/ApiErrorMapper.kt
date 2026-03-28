package com.estacionamento.parking.errors

/** SPEC_FRONTEND §8 — fallback quando message vazio. */
object ApiErrorMapper {
    private val map = mapOf(
        "VALIDATION_ERROR" to "Verifique os dados informados.",
        "UNAUTHORIZED" to "Sessão expirada. Faça login novamente.",
        "FORBIDDEN" to "Você não tem permissão.",
        "NOT_FOUND" to "Registro não encontrado.",
        "CONFLICT" to "Operação não permitida no estado atual.",
        "PLATE_INVALID" to "Formato de placa inválido.",
        "PLATE_HAS_ACTIVE_TICKET" to "Já existe ticket em aberto para esta placa.",
        "INVALID_TICKET_STATE" to "Ticket não está nesta etapa.",
        "LOJISTA_WALLET_MISSING" to "Convênio indisponível: carteira do lojista não configurada.",
        "PAYMENT_ALREADY_PAID" to "Pagamento já confirmado.",
        "AMOUNT_MISMATCH" to "Valor não confere.",
        "CASH_SESSION_REQUIRED" to "Abra o caixa antes de receber em dinheiro.",
        "OPERATOR_BLOCKED" to "Operador bloqueado. Procure o gestor.",
        "TENANT_UNAVAILABLE" to "Estacionamento indisponível. Tente mais tarde.",
        "LOGIN_THROTTLED" to "Muitas tentativas. Aguarde e tente novamente.",
        "CLOCK_SKEW" to "Relógio do aparelho incorreto. Ajuste a data e hora.",
        "INTERNAL" to "Erro no servidor. Tente novamente.",
    )

    fun messageForCode(code: String?): String? = code?.let { map[it] }

    fun extractCode(errorJson: String?): String? {
        if (errorJson.isNullOrBlank()) return null
        return extractField(errorJson, "code")
    }

    fun resolve(errorJson: String?): String {
        if (errorJson.isNullOrBlank()) return "Erro inesperado."
        val msg = extractField(errorJson, "message")
        if (!msg.isNullOrBlank()) return msg
        val code = extractField(errorJson, "code")
        return messageForCode(code) ?: "Erro inesperado."
    }

    private fun extractField(json: String, field: String): String? {
        val re = """"$field"\s*:\s*"((?:\\.|[^"\\])*)"""".toRegex()
        return re.find(json)?.groupValues?.get(1)?.replace("\\\"", "\"")?.trim()
    }
}
