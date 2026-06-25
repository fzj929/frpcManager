<template>
  <div class="wake-mac-page">
    <div class="page-header">
      <h2>MAC 地址管理</h2>
      <div class="header-actions">
        <el-button :icon="Refresh" @click="loadItems">刷新</el-button>
        <el-button type="primary" :icon="Plus" @click="openDialog()">新增 MAC 地址</el-button>
      </div>
    </div>

    <el-card class="table-card">
      <el-table :data="items" v-loading="loading" row-key="id" stripe>
        <el-table-column prop="name" label="主机名称" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <span>{{ row.name }}</span>
            <el-tag v-if="row.name === row.macAddress" size="small" type="info" class="name-tag">未命名</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="macAddress" label="MAC 地址" min-width="170" />
        <el-table-column label="创建时间" min-width="170">
          <template #default="{ row }">
            {{ formatDateTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="更新时间" min-width="170">
          <template #default="{ row }">
            {{ formatDateTime(row.updatedAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" :icon="SwitchButton" :loading="wakingId === row.id" @click="wakeItem(row)">
              唤醒
            </el-button>
            <el-button size="small" :icon="Edit" @click="openDialog(row)">编辑</el-button>
            <el-button size="small" type="danger" :icon="Delete" @click="removeItem(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 MAC 地址' : '新增 MAC 地址'" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="MAC 地址" prop="macAddress">
          <el-input v-model.trim="form.macAddress" placeholder="00:11:22:33:44:55" />
          <div class="form-hint">支持 00:11:22:33:44:55、00-11-22-33-44-55、001122334455、0011.2233.4455。</div>
        </el-form-item>
        <el-form-item label="主机名称">
          <el-input v-model.trim="form.name" placeholder="例如：张三的主机；留空则默认使用 MAC 地址" maxlength="100" show-word-limit />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="saveItem">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Delete, Edit, Plus, Refresh, SwitchButton } from '@element-plus/icons-vue'
import {
  createWakeMacAddress,
  deleteWakeMacAddress,
  fetchWakeMacAddresses,
  updateWakeMacAddress,
  wakeOnLan
} from '@/api'
import type { WakeMacAddress } from '@/types'
import { formatDateTime } from '@/utils/date'

const items = ref<WakeMacAddress[]>([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const wakingId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const form = reactive({
  macAddress: '',
  name: ''
})

const macPattern = /^([0-9a-fA-F]{2}[:-]?){5}[0-9a-fA-F]{2}$|^[0-9a-fA-F]{12}$|^[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}$/

const rules: FormRules = {
  macAddress: [
    { required: true, message: '请输入 MAC 地址', trigger: 'blur' },
    { pattern: macPattern, message: 'MAC 地址格式不正确', trigger: 'blur' }
  ]
}

async function loadItems() {
  loading.value = true
  try {
    const res = await fetchWakeMacAddresses()
    items.value = res.data
  } finally {
    loading.value = false
  }
}

function openDialog(row?: WakeMacAddress) {
  editingId.value = row?.id ?? null
  form.macAddress = row?.macAddress ?? ''
  form.name = row && row.name !== row.macAddress ? row.name : ''
  dialogVisible.value = true
  formRef.value?.clearValidate()
}

async function saveItem() {
  if (!await formRef.value?.validate().catch(() => false)) return

  saving.value = true
  try {
    const data = { macAddress: form.macAddress, name: form.name }
    if (editingId.value) {
      await updateWakeMacAddress(editingId.value, data)
      ElMessage.success('MAC 地址已更新')
    } else {
      await createWakeMacAddress(data)
      ElMessage.success('MAC 地址已添加')
    }
    dialogVisible.value = false
    await loadItems()
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '保存失败'
    ElMessage.error(msg)
  } finally {
    saving.value = false
  }
}

async function removeItem(row: WakeMacAddress) {
  await ElMessageBox.confirm(`确定要删除「${row.name}」吗？历史记录和定时任务中的 MAC 地址不会被删除。`, '提示', { type: 'warning' })
  await deleteWakeMacAddress(row.id)
  ElMessage.success('MAC 地址已删除')
  await loadItems()
}

async function wakeItem(row: WakeMacAddress) {
  wakingId.value = row.id
  try {
    const res = await wakeOnLan({
      macAddress: row.macAddress,
      broadcastAddress: '255.255.255.255',
      port: 9
    })
    ElMessage.success(res.data?.message ?? '魔术数据包已发送')
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '发送失败'
    ElMessage.error(msg)
  } finally {
    wakingId.value = null
  }
}

onMounted(loadItems)
</script>

<style scoped>
.wake-mac-page {
  max-width: 1400px;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  gap: 12px;
}

.page-header h2 {
  margin: 0;
  color: #303133;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.table-card {
  border-radius: 8px;
}

.name-tag {
  margin-left: 8px;
}

.form-hint {
  margin-top: 6px;
  color: #909399;
  font-size: 12px;
  line-height: 1.4;
}

@media (max-width: 768px) {
  .wake-mac-page {
    max-width: none;
  }

  .page-header {
    align-items: stretch;
    flex-direction: column;
  }

  .header-actions {
    width: 100%;
  }

  .header-actions .el-button {
    flex: 1;
  }
}
</style>
