<template>
  <div class="audit-page">
    <div class="page-header">
      <h2>操作日志</h2>
      <el-button :icon="Refresh" :loading="loading" @click="loadLogs">刷新</el-button>
    </div>

    <el-card class="audit-card">
      <el-table :data="logs" v-loading="loading" row-key="id" stripe>
        <el-table-column label="时间" prop="createdAt" width="180">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="用户" prop="username" width="120" />
        <el-table-column label="操作" prop="action" min-width="170" />
        <el-table-column label="对象" prop="target" min-width="140" />
        <el-table-column label="结果" width="90">
          <template #default="{ row }">
            <el-tag :type="row.success ? 'success' : 'danger'" size="small">
              {{ row.success ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="IP" prop="ipAddress" width="140" />
        <el-table-column label="详情" prop="details" min-width="220" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import { fetchAuditLogs } from '@/api'
import type { AuditLog } from '@/types'
import { formatDateTime as formatTime } from '@/utils/date'

const logs = ref<AuditLog[]>([])
const loading = ref(false)

async function loadLogs() {
  loading.value = true
  try {
    const res = await fetchAuditLogs(300)
    logs.value = res.data
  } catch {
    ElMessage.error('加载操作日志失败')
  } finally {
    loading.value = false
  }
}

onMounted(loadLogs)
</script>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.page-header h2 {
  margin: 0;
  font-size: 22px;
  color: #1a1a2e;
}

.audit-card {
  border-radius: 12px;
}

@media (max-width: 768px) {
  .page-header {
    margin-bottom: 14px;
  }

  .page-header h2 {
    font-size: 20px;
  }

  .audit-card :deep(.el-card__body) {
    overflow-x: auto;
  }

  .audit-card :deep(.el-table) {
    min-width: 980px;
  }
}
</style>
