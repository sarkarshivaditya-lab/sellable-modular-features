package com.udc.collection.data.local.dao

import androidx.room.*
import com.udc.collection.data.local.entity.LabPackageEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface LabPackageDao {

    @Query("SELECT * FROM lab_packages WHERE isActive = 1 ORDER BY name ASC")
    fun getAllPackages(): Flow<List<LabPackageEntity>>

    @Query("""
        SELECT * FROM lab_packages
        WHERE isActive = 1 AND (name LIKE '%' || :query || '%' OR description LIKE '%' || :query || '%')
        ORDER BY name ASC
    """)
    fun searchPackages(query: String): Flow<List<LabPackageEntity>>

    @Query("SELECT * FROM lab_packages WHERE id = :id")
    suspend fun getPackageById(id: Long): LabPackageEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertPackage(pkg: LabPackageEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertPackages(packages: List<LabPackageEntity>)

    @Update
    suspend fun updatePackage(pkg: LabPackageEntity)

    @Delete
    suspend fun deletePackage(pkg: LabPackageEntity)

    @Query("SELECT COUNT(*) FROM lab_packages")
    suspend fun getPackageCount(): Int

    @Query("DELETE FROM lab_packages")
    suspend fun deleteAllPackages()
}
