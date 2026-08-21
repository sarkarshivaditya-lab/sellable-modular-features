package com.udc.collection.domain.model

data class SelectedTest(
    val testId: Long,
    val testName: String,
    val price: Double,
    val isPackage: Boolean = false
)

enum class DiscountType { NONE, PERCENTAGE, FLAT }
enum class PaymentStatus(val label: String) { PAID("Paid"), UNPAID("Unpaid"), PARTIAL("Partial") }
enum class PaymentMethod(val label: String) { CASH("Cash"), UPI("UPI"), CARD("Card"), CREDIT("Credit") }
