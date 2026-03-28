package com.estacionamento.parking.ui.common



import android.graphics.Bitmap

import androidx.compose.ui.graphics.ImageBitmap

import androidx.compose.ui.graphics.asImageBitmap

import com.google.zxing.BarcodeFormat

import com.google.zxing.qrcode.QRCodeWriter



object QrBitmap {

    fun encode(content: String, size: Int = 512): ImageBitmap {

        val matrix = QRCodeWriter().encode(content, BarcodeFormat.QR_CODE, size, size)

        val bmp = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888)

        for (x in 0 until size) {

            for (y in 0 until size) {

                bmp.setPixel(x, y, if (matrix.get(x, y)) android.graphics.Color.BLACK else android.graphics.Color.WHITE)

            }

        }

        return bmp.asImageBitmap()

    }

}


