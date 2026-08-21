package com.udc.collection.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import com.udc.collection.data.local.dao.LabTestDao
import com.udc.collection.data.local.dao.PatientDao
import com.udc.collection.data.local.entity.LabTestEntity
import com.udc.collection.data.local.entity.PatientConverters
import com.udc.collection.data.local.entity.PatientEntity

@Database(
    entities = [PatientEntity::class, LabTestEntity::class],
    version = 3,
    exportSchema = true
)
@TypeConverters(PatientConverters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun patientDao(): PatientDao
    abstract fun labTestDao(): LabTestDao

    companion object {
        const val DATABASE_NAME = "udc_collection.db"
    }
}
