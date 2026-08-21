package com.udc.collection.data.local.dao

import androidx.room.*
import com.udc.collection.data.local.entity.PatientEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface PatientDao {

    @Query("SELECT * FROM patients ORDER BY createdAt DESC")
    fun getAllPatients(): Flow<List<PatientEntity>>

    @Query("""
        SELECT * FROM patients
        WHERE name LIKE '%' || :query || '%'
        OR phone LIKE '%' || :query || '%'
        OR patientNumber LIKE '%' || :query || '%'
        OR date LIKE '%' || :query || '%'
        ORDER BY createdAt DESC
    """)
    fun searchPatients(query: String): Flow<List<PatientEntity>>

    @Query("SELECT * FROM patients WHERE id = :id")
    suspend fun getPatientById(id: Long): PatientEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertPatient(patient: PatientEntity): Long

    @Update
    suspend fun updatePatient(patient: PatientEntity)

    @Delete
    suspend fun deletePatient(patient: PatientEntity)

    @Query("SELECT COUNT(*) FROM patients")
    suspend fun getPatientCount(): Int

    @Query("SELECT * FROM patients ORDER BY createdAt DESC LIMIT 1")
    suspend fun getLatestPatient(): PatientEntity?

    @Query("SELECT DISTINCT referringDoctor FROM patients WHERE referringDoctor != '' ORDER BY createdAt DESC LIMIT 20")
    fun getRecentDoctors(): Flow<List<String>>

    @Query("SELECT DISTINCT address FROM patients WHERE address != '' ORDER BY createdAt DESC LIMIT 20")
    fun getRecentAddresses(): Flow<List<String>>

    @Query("DELETE FROM patients")
    suspend fun deleteAllPatients()

    // Dashboard queries
    @Query("SELECT COUNT(*) FROM patients WHERE date = :today")
    fun getTodayPatientCount(today: String): Flow<Int>

    @Query("SELECT COALESCE(SUM(grandTotal), 0.0) FROM patients WHERE date = :today AND paymentStatus = 'PAID'")
    fun getTodayRevenue(today: String): Flow<Double>

    @Query("SELECT COALESCE(SUM(grandTotal), 0.0) FROM patients WHERE date = :today")
    fun getTodayTotalBilled(today: String): Flow<Double>

    @Query("SELECT COUNT(*) FROM patients WHERE paymentStatus IN ('UNPAID', 'PARTIAL')")
    fun getPendingPaymentCount(): Flow<Int>

    @Query("SELECT COALESCE(SUM(grandTotal - amountReceived), 0.0) FROM patients WHERE paymentStatus IN ('UNPAID', 'PARTIAL')")
    fun getTotalOutstanding(): Flow<Double>

    // Receipt number: count receipts already issued for the given date prefix
    @Query("SELECT COUNT(*) FROM patients WHERE receiptNumber LIKE :prefix || '%'")
    suspend fun countReceiptsForDate(prefix: String): Int

    @Query("SELECT * FROM patients WHERE date = :date ORDER BY createdAt ASC")
    suspend fun getPatientsForDate(date: String): List<PatientEntity>

    @Query("SELECT * FROM patients ORDER BY createdAt DESC LIMIT :limit")
    fun getRecentPatients(limit: Int = 5): Flow<List<PatientEntity>>
}
