package com.udc.collection.data.local.dao

import androidx.room.*
import com.udc.collection.data.local.entity.DraftPatientEntity

@Dao
interface DraftDao {

    @Query("SELECT * FROM draft_patient WHERE id = 1")
    suspend fun getDraft(): DraftPatientEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun saveDraft(draft: DraftPatientEntity)

    @Query("DELETE FROM draft_patient")
    suspend fun clearDraft()
}
