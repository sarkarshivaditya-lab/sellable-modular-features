package com.udc.collection.domain.model

import java.time.LocalDate

data class Patient(
    val id: Long = 0,
    val patientNumber: String = "",
    val name: String,
    val age: String = "",
    val gender: String = "",
    val phone: String = "",
    val address: String = "",
    val referringDoctor: String = "",
    val date: LocalDate = LocalDate.now(),
    val remarks: String = "",
    val selectedTests: List<SelectedTest> = emptyList(),
    val discountType: DiscountType = DiscountType.NONE,
    val discountValue: Double = 0.0,
    val subtotal: Double = 0.0,
    val grandTotal: Double = 0.0,
    val paymentStatus: PaymentStatus = PaymentStatus.UNPAID,
    val paymentMethod: PaymentMethod = PaymentMethod.CASH,
    val amountReceived: Double = 0.0,
    val receiptNumber: String = "",
    val createdAt: Long = System.currentTimeMillis()
)

data class SelectedTest(
    val testId: Long,
    val testName: String,
    val price: Double,
    val isPackage: Boolean = false
)

enum class DiscountType { NONE, PERCENTAGE, FLAT }
enum class PaymentStatus(val label: String) { PAID("Paid"), UNPAID("Unpaid"), PARTIAL("Partial") }
enum class PaymentMethod(val label: String) { CASH("Cash"), UPI("UPI"), CARD("Card"), CREDIT("Credit") }
