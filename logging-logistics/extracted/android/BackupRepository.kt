package com.udc.collection.data.repository

import android.content.Context
import android.net.Uri
import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.google.gson.reflect.TypeToken
import com.udc.collection.data.local.AppDatabase
import com.udc.collection.data.local.entity.LabTestEntity
import com.udc.collection.data.local.entity.PatientEntity
import com.udc.collection.domain.model.SelectedTest
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.withContext
import java.io.FileOutputStream
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter
import javax.inject.Inject
import javax.inject.Singleton

enum class CsvFilter { TODAY, ALL, PENDING }
data class BackupData(val version: Int = 1, val exportedAt: String, val patients: List<PatientEntity>, val labTests: List<LabTestEntity>)
sealed class BackupResult { data class Success(val message: String) : BackupResult(); data class Error(val message: String) : BackupResult() }

@Singleton
class BackupRepository @Inject constructor(@ApplicationContext private val context: Context, private val database: AppDatabase) {
    private val gson: Gson = GsonBuilder().setPrettyPrinting().create()
    suspend fun exportDatabaseFile(uri: Uri): BackupResult = withContext(Dispatchers.IO) { runCatching { val dbFile = context.getDatabasePath(AppDatabase.DATABASE_NAME); database.openHelper.writableDatabase.execSQL("PRAGMA wal_checkpoint(TRUNCATE)"); context.contentResolver.openOutputStream(uri)?.use { out -> dbFile.inputStream().use { it.copyTo(out) } } ?: return@withContext BackupResult.Error("Could not open output stream"); BackupResult.Success("Database exported successfully") }.getOrElse { BackupResult.Error("Export failed: ${it.localizedMessage}") } }
    suspend fun importDatabaseFile(uri: Uri): BackupResult = withContext(Dispatchers.IO) { runCatching { val dbFile = context.getDatabasePath(AppDatabase.DATABASE_NAME); database.close(); context.contentResolver.openInputStream(uri)?.use { input -> FileOutputStream(dbFile).use { input.copyTo(it) } } ?: return@withContext BackupResult.Error("Could not open input stream"); BackupResult.Success("Database imported. Please restart the app.") }.getOrElse { BackupResult.Error("Import failed: ${it.localizedMessage}") } }
    suspend fun exportToJson(uri: Uri): BackupResult = withContext(Dispatchers.IO) { runCatching { val customers = database.patientDao().getAllPatients().first(); val services = database.labTestDao().getAllTests().first(); val backup = BackupData(exportedAt = LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME), patients = customers, labTests = services); context.contentResolver.openOutputStream(uri)?.use { it.write(gson.toJson(backup).toByteArray()) } ?: return@withContext BackupResult.Error("Could not open output stream"); BackupResult.Success("Exported ${backup.patients.size} customers and ${backup.labTests.size} services") }.getOrElse { BackupResult.Error("Export failed: ${it.localizedMessage}") } }
    suspend fun importFromJson(uri: Uri): BackupResult = withContext(Dispatchers.IO) { runCatching { val json = context.contentResolver.openInputStream(uri)?.use { it.bufferedReader().readText() } ?: return@withContext BackupResult.Error("Could not read file"); val backup: BackupData = gson.fromJson(json, object : TypeToken<BackupData>() {}.type) ?: return@withContext BackupResult.Error("Invalid backup format"); database.patientDao().deleteAllPatients(); database.labTestDao().deleteAllTests(); backup.patients.forEach { database.patientDao().insertPatient(it) }; backup.labTests.forEach { database.labTestDao().insertTest(it) }; BackupResult.Success("Imported ${backup.patients.size} customers and ${backup.labTests.size} services") }.getOrElse { BackupResult.Error("Import failed: ${it.localizedMessage}") } }
    suspend fun exportPatientsToCSV(uri: Uri, filter: CsvFilter): BackupResult = withContext(Dispatchers.IO) { runCatching { val allCustomers = database.patientDao().getAllPatients().first(); val today = DateTimeFormatter.ISO_LOCAL_DATE.format(LocalDate.now()); val customers = when (filter) { CsvFilter.TODAY -> allCustomers.filter { it.date == today }; CsvFilter.ALL -> allCustomers; CsvFilter.PENDING -> allCustomers.filter { it.paymentStatus in listOf("UNPAID", "PARTIAL") } }; val serviceType = object : TypeToken<List<SelectedTest>>() {}.type; val header = "Receipt No,Customer No,Name,Phone,Date,Services,Subtotal,Discount,Grand Total,Payment Status,Payment Method,Amount Received,Outstanding\n"; context.contentResolver.openOutputStream(uri)?.bufferedWriter()?.use { writer -> writer.write(header); customers.forEach { p -> val services = runCatching { gson.fromJson<List<SelectedTest>>(p.selectedTests, serviceType) }.getOrDefault(emptyList()); val serviceNames = services.joinToString("; ") { it.testName }.csv(); val outstanding = p.grandTotal - p.amountReceived; writer.write("${p.receiptNumber.csv()},${p.patientNumber.csv()},${p.name.csv()},${p.phone.csv()},${p.date},$serviceNames,${p.subtotal},${p.discountValue},${p.grandTotal},${p.paymentStatus},${p.paymentMethod},${p.amountReceived},$outstanding\n") } } ?: return@withContext BackupResult.Error("Could not open output stream"); BackupResult.Success("Exported ${customers.size} records to CSV") }.getOrElse { BackupResult.Error("CSV export failed: ${it.localizedMessage}") } }
    private fun String.csv(): String = if (contains(',') || contains('"') || contains('\n')) "\"${replace("\"", "\"\"")}\"" else this
}
