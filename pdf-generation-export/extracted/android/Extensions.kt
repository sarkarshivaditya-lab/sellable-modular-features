package com.udc.collection.util

import java.text.NumberFormat
import java.util.Locale

private val currencyFormat = NumberFormat.getCurrencyInstance(Locale("en", "IN"))

fun Double.formatCurrency(): String {
    return "₹${String.format(Locale.ENGLISH, "%.2f", this)}"
}
