package com.udc.collection.data.local.entity

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import androidx.room.TypeConverter
import androidx.room.TypeConverters
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.udc.collection.domain.model.DiscountType
import com.udc.collection.domain.model.PaymentMethod
import com.udc.collection.domain.model.PaymentStatus
import com.udc.collection.domain.model.SelectedTest

@Entity(
    tableName = "patients",
    indices = [
        Index(value = ["date"]),
        Index(value = ["paymentStatus"]),
        Index(value = ["createdAt"])
    ]
)
@TypeConverters(PatientConverters::class)
data class PatientEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val patientNumber: String,
    val name: String,
    val age: String,
    val gender: String,
    val phone: String,
    val address: String,
    val referringDoctor: String,
    val date: String,
    val remarks: String,
    val selectedTests: String,
    val discountType: String,
    val discountValue: Double,
    val subtotal: Double,
    val grandTotal: Double,
    // Payment fields — added in migration v2
    val paymentStatus: String = PaymentStatus.UNPAID.name,
    val paymentMethod: String = PaymentMethod.CASH.name,
    val amountReceived: Double = 0.0,
    val receiptNumber: String = "",
    val createdAt: Long = System.currentTimeMillis()
)

@Entity(
    tableName = "lab_tests",
    indices = [Index(value = ["useCount"])]
)
data class LabTestEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val name: String,
    val price: Double,
    val category: String,
    val isCustom: Boolean = false,
    // Usage tracking — added in migration v2
    val useCount: Int = 0
)

@Entity(tableName = "lab_packages")
data class LabPackageEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val name: String,
    val description: String = "",
    val price: Double,
    val testIds: String = "[]",       // JSON array of LabTest IDs
    val testNames: String = "[]",     // JSON array of names (denormalized for display)
    val isActive: Boolean = true
)

// Single-row table for in-progress wizard draft
@Entity(tableName = "draft_patient")
data class DraftPatientEntity(
    @PrimaryKey
    val id: Int = 1,
    val json: String,
    val updatedAt: Long = System.currentTimeMillis()
)

class PatientConverters {
    private val gson = Gson()

    @TypeConverter
    fun fromSelectedTests(value: String): List<SelectedTest> {
        val type = object : TypeToken<List<SelectedTest>>() {}.type
        return runCatching { gson.fromJson<List<SelectedTest>>(value, type) }.getOrDefault(emptyList())
    }

    @TypeConverter
    fun toSelectedTests(list: List<SelectedTest>): String = gson.toJson(list)

    @TypeConverter
    fun fromDiscountType(value: String): DiscountType =
        runCatching { DiscountType.valueOf(value) }.getOrDefault(DiscountType.NONE)

    @TypeConverter
    fun toDiscountType(type: DiscountType): String = type.name
}
