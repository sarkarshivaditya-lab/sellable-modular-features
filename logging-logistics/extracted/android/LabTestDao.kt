package com.udc.collection.data.local.dao

import androidx.room.*
import com.udc.collection.data.local.entity.LabTestEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface LabTestDao {

    @Query("SELECT * FROM lab_tests ORDER BY category ASC, name ASC")
    fun getAllTests(): Flow<List<LabTestEntity>>

    @Query("""
        SELECT * FROM lab_tests
        WHERE name LIKE '%' || :query || '%'
        OR category LIKE '%' || :query || '%'
        ORDER BY name ASC
    """)
    fun searchTests(query: String): Flow<List<LabTestEntity>>

    @Query("SELECT * FROM lab_tests ORDER BY useCount DESC, name ASC LIMIT :limit")
    fun getFrequentlyUsedTests(limit: Int = 10): Flow<List<LabTestEntity>>

    @Query("SELECT * FROM lab_tests WHERE id = :id")
    suspend fun getTestById(id: Long): LabTestEntity?

    @Query("SELECT * FROM lab_tests WHERE id IN (:ids)")
    suspend fun getTestsByIds(ids: List<Long>): List<LabTestEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertTest(test: LabTestEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertTests(tests: List<LabTestEntity>)

    @Update
    suspend fun updateTest(test: LabTestEntity)

    @Delete
    suspend fun deleteTest(test: LabTestEntity)

    @Query("SELECT COUNT(*) FROM lab_tests")
    suspend fun getTestCount(): Int

    @Query("DELETE FROM lab_tests")
    suspend fun deleteAllTests()

    @Query("UPDATE lab_tests SET useCount = useCount + 1 WHERE id IN (:ids)")
    suspend fun incrementUseCount(ids: List<Long>)
}
