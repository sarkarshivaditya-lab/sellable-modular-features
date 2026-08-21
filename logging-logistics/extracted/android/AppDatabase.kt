package com.udc.collection.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import com.udc.collection.data.local.dao.DraftDao
import com.udc.collection.data.local.dao.LabPackageDao
import com.udc.collection.data.local.dao.LabTestDao
import com.udc.collection.data.local.dao.PatientDao
import com.udc.collection.data.local.entity.DraftPatientEntity
import com.udc.collection.data.local.entity.LabPackageEntity
import com.udc.collection.data.local.entity.LabTestEntity
import com.udc.collection.data.local.entity.PatientConverters
import com.udc.collection.data.local.entity.PatientEntity

@Database(
    entities = [
        PatientEntity::class,
        LabTestEntity::class,
        LabPackageEntity::class,
        DraftPatientEntity::class
    ],
    version = 3,
    exportSchema = true
)
@TypeConverters(PatientConverters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun patientDao(): PatientDao
    abstract fun labTestDao(): LabTestDao
    abstract fun labPackageDao(): LabPackageDao
    abstract fun draftDao(): DraftDao

    companion object {
        const val DATABASE_NAME = "udc_collection.db"

        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE patients ADD COLUMN paymentStatus TEXT NOT NULL DEFAULT 'UNPAID'")
                db.execSQL("ALTER TABLE patients ADD COLUMN paymentMethod TEXT NOT NULL DEFAULT 'CASH'")
                db.execSQL("ALTER TABLE patients ADD COLUMN amountReceived REAL NOT NULL DEFAULT 0.0")
                db.execSQL("ALTER TABLE patients ADD COLUMN receiptNumber TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE lab_tests ADD COLUMN useCount INTEGER NOT NULL DEFAULT 0")
                db.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS lab_packages (
                        id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                        name TEXT NOT NULL,
                        description TEXT NOT NULL DEFAULT '',
                        price REAL NOT NULL,
                        testIds TEXT NOT NULL DEFAULT '[]',
                        testNames TEXT NOT NULL DEFAULT '[]',
                        isActive INTEGER NOT NULL DEFAULT 1
                    )
                    """.trimIndent()
                )
                db.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS draft_patient (
                        id INTEGER PRIMARY KEY NOT NULL,
                        json TEXT NOT NULL,
                        updatedAt INTEGER NOT NULL
                    )
                    """.trimIndent()
                )
            }
        }

        val MIGRATION_2_3 = object : Migration(2, 3) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("CREATE INDEX IF NOT EXISTS index_patients_date ON patients(date)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_patients_paymentStatus ON patients(paymentStatus)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_patients_createdAt ON patients(createdAt)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_lab_tests_useCount ON lab_tests(useCount)")
            }
        }
    }
}
