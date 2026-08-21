package com.udc.collection.di

import android.content.Context
import androidx.room.Room
import com.udc.collection.data.local.AppDatabase
import com.udc.collection.data.local.dao.DraftDao
import com.udc.collection.data.local.dao.LabPackageDao
import com.udc.collection.data.local.dao.LabTestDao
import com.udc.collection.data.local.dao.PatientDao
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideDatabase(@ApplicationContext context: Context): AppDatabase =
        Room.databaseBuilder(context, AppDatabase::class.java, AppDatabase.DATABASE_NAME)
            .addMigrations(AppDatabase.MIGRATION_1_2, AppDatabase.MIGRATION_2_3)
            .build()

    @Provides fun providePatientDao(db: AppDatabase): PatientDao = db.patientDao()
    @Provides fun provideLabTestDao(db: AppDatabase): LabTestDao = db.labTestDao()
    @Provides fun provideLabPackageDao(db: AppDatabase): LabPackageDao = db.labPackageDao()
    @Provides fun provideDraftDao(db: AppDatabase): DraftDao = db.draftDao()
}
