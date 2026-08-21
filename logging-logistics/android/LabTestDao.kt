package com.udc.collection.data.local.dao

import androidx.room.*
import com.udc.collection.data.local.entity.LabTestEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface LabTestDao {
    @Query("SELECT * FROM lab_tests ORDER BY name")
    fun getAllTests(): Flow<List<LabTestEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertTest(test: LabTestEntity): Long

    @Query("DELETE FROM lab_tests")
    suspend fun deleteAllTests()
}
