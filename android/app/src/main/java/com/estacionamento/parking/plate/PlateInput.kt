package com.estacionamento.parking.plate

import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation

object PlateInput {
    /** Mesma regra que o web: até 7 caracteres válidos (Mercosul ou legado). */
    fun sanitize(raw: String): String {
        val cleaned = PlateValidator.normalize(raw).filter { it.isLetterOrDigit() }
        val sb = StringBuilder()
        for (c in cleaned) {
            if (sb.length >= 7) break
            val pos = sb.length
            when {
                pos < 3 -> {
                    if (c in 'A'..'Z') sb.append(c)
                }
                pos == 3 -> {
                    if (c in '0'..'9') sb.append(c)
                }
                pos == 4 -> {
                    if (c in 'A'..'Z' || c in '0'..'9') sb.append(c)
                }
                else -> {
                    if (c in '0'..'9') sb.append(c)
                }
            }
        }
        return sb.toString()
    }
}

/** Exibe AAA-XXXX; o valor do campo permanece sem hífen. */
object PlateMaskVisualTransformation : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText {
        val s = text.text
        val out = AnnotatedString(
            if (s.length <= 3) s else "${s.substring(0, 3)}-${s.substring(3)}",
        )
        val offsetMapping =
            object : OffsetMapping {
                override fun originalToTransformed(offset: Int): Int =
                    when {
                        offset <= 0 -> 0
                        offset <= 3 -> offset
                        else -> (offset + 1).coerceAtMost(out.length)
                    }

                override fun transformedToOriginal(offset: Int): Int =
                    when {
                        offset <= 0 -> 0
                        offset <= 3 -> offset.coerceAtMost(s.length)
                        offset == 4 && s.length >= 3 -> 3
                        else -> (offset - 1).coerceAtMost(s.length)
                    }
            }
        return TransformedText(out, offsetMapping)
    }
}
