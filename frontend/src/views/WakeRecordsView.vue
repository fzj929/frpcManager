<template>
  <div class="wake-records-page">
    <div class="page-header">
      <h2>唤醒记录</h2>
      <div class="header-actions">
        <el-button :icon="Refresh" @click="loadAll">刷新</el-button>
        <el-button type="primary" :icon="Plus" @click="openScheduleDialog()">新增定时唤醒</el-button>
      </div>
    </div>

    <el-card class="section-card">
      <template #header>
        <div class="card-header">
          <el-icon><Timer /></el-icon>
          <span>定时唤醒</span>
        </div>
      </template>

      <el-table :data="schedules" v-loading="scheduleLoading" row-key="id" stripe>
        <el-table-column prop="name" label="任务名称" min-width="140" />
        <el-table-column prop="macName" label="主机名称" min-width="140" show-overflow-tooltip />
        <el-table-column prop="macAddress" label="MAC 地址" min-width="150" />
        <el-table-column prop="broadcastAddress" label="广播地址" min-width="130" />
        <el-table-column prop="port" label="端口" width="80" />
        <el-table-column prop="timeOfDay" label="时间" width="100" />
        <el-table-column label="执行规则" min-width="180">
          <template #default="{ row }">
            {{ formatScheduleRule(row) }}
          </template>
        </el-table-column>
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'">
              {{ row.isEnabled ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="上次执行" min-width="170">
          <template #default="{ row }">
            {{ row.lastRunAt ? new Date(row.lastRunAt).toLocaleString() : '-' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button size="small" :icon="SwitchButton" :loading="wakingId === `schedule-${row.id}`" @click="wakeSchedule(row.id)">
              唤醒
            </el-button>
            <el-button size="small" :icon="Edit" @click="openScheduleDialog(row)">编辑</el-button>
            <el-button size="small" type="danger" :icon="Delete" @click="removeSchedule(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card class="section-card">
      <template #header>
        <div class="card-header">
          <el-icon><Document /></el-icon>
          <span>唤醒历史</span>
        </div>
      </template>

      <el-table :data="logs" v-loading="logLoading" row-key="id" stripe>
        <el-table-column label="时间" min-width="170">
          <template #default="{ row }">
            {{ new Date(row.createdAt).toLocaleString() }}
          </template>
        </el-table-column>
        <el-table-column prop="macName" label="主机名称" min-width="140" show-overflow-tooltip />
        <el-table-column prop="macAddress" label="MAC 地址" min-width="150" />
        <el-table-column prop="broadcastAddress" label="广播地址" min-width="130" />
        <el-table-column prop="port" label="端口" width="80" />
        <el-table-column label="来源" width="100">
          <template #default="{ row }">
            <el-tag :type="row.source === 'schedule' ? 'warning' : 'primary'">
              {{ row.source === 'schedule' ? '定时' : '手动' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="结果" width="90">
          <template #default="{ row }">
            <el-tag :type="row.success ? 'success' : 'danger'">
              {{ row.success ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="message" label="消息" min-width="160" show-overflow-tooltip />
        <el-table-column prop="username" label="用户" min-width="110" />
        <el-table-column prop="ipAddress" label="来源 IP" min-width="130" />
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" :icon="SwitchButton" :loading="wakingId === `log-${row.id}`" @click="wakeLog(row.id)">
              再次唤醒
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="scheduleDialogVisible" :title="editingScheduleId ? '编辑定时唤醒' : '新增定时唤醒'" width="520px">
      <el-form ref="scheduleFormRef" :model="scheduleForm" :rules="scheduleRules" label-width="100px">
        <el-form-item label="任务名称" prop="name">
          <el-input v-model.trim="scheduleForm.name" placeholder="例如：办公室电脑" />
        </el-form-item>
        <el-form-item label="MAC 地址" prop="macAddress">
          <el-select
            v-model.trim="scheduleForm.macAddress"
            filterable
            allow-create
            default-first-option
            placeholder="00:11:22:33:44:55"
            style="width: 100%"
          >
            <el-option
              v-for="item in macAddresses"
              :key="item.id"
              :label="macOptionLabel(item)"
              :value="item.macAddress"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="广播地址" prop="broadcastAddress">
          <el-input v-model.trim="scheduleForm.broadcastAddress" placeholder="255.255.255.255" />
        </el-form-item>
        <el-form-item label="端口" prop="port">
          <el-input-number v-model="scheduleForm.port" :min="1" :max="65535" style="width: 100%" />
        </el-form-item>
        <el-form-item label="每天时间" prop="timeOfDay">
          <el-time-picker v-model="scheduleTime" format="HH:mm" value-format="HH:mm" placeholder="选择时间" style="width: 100%" />
        </el-form-item>
        <el-form-item label="执行方式" prop="scheduleMode">
          <el-radio-group v-model="scheduleForm.scheduleMode">
            <el-radio-button label="daily">每天</el-radio-button>
            <el-radio-button label="weekly">每周</el-radio-button>
            <el-radio-button label="date">指定日期</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="scheduleForm.scheduleMode === 'weekly'" label="选择星期" prop="daysOfWeek">
          <el-checkbox-group v-model="scheduleForm.daysOfWeek">
            <el-checkbox-button
              v-for="day in weekDays"
              :key="day.value"
              :label="day.value"
            >
              {{ day.label }}
            </el-checkbox-button>
          </el-checkbox-group>
        </el-form-item>
        <el-form-item v-if="scheduleForm.scheduleMode === 'date'" label="指定日期" prop="specificDate">
          <el-date-picker
            v-model="scheduleForm.specificDate"
            type="date"
            value-format="YYYY-MM-DD"
            placeholder="选择日期"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="scheduleForm.isEnabled" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="scheduleDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="savingSchedule" @click="saveSchedule">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Delete, Document, Edit, Plus, Refresh, SwitchButton, Timer } from '@element-plus/icons-vue'
import {
  createWakeSchedule,
  deleteWakeSchedule,
  fetchWakeMacAddresses,
  fetchWakeLogs,
  fetchWakeSchedules,
  updateWakeSchedule,
  wakeFromLog,
  wakeFromSchedule
} from '@/api'
import type { WakeLog, WakeMacAddress, WakeSchedule } from '@/types'

const logs = ref<WakeLog[]>([])
const schedules = ref<WakeSchedule[]>([])
const macAddresses = ref<WakeMacAddress[]>([])
const logLoading = ref(false)
const scheduleLoading = ref(false)
const savingSchedule = ref(false)
const wakingId = ref('')
const scheduleDialogVisible = ref(false)
const editingScheduleId = ref<number | null>(null)
const scheduleFormRef = ref<FormInstance>()
const scheduleTime = ref('08:00')

const scheduleForm = reactive({
  name: '',
  macAddress: '',
  broadcastAddress: '255.255.255.255',
  port: 9,
  scheduleMode: 'daily',
  daysOfWeek: [] as string[],
  specificDate: '',
  isEnabled: true
})

const macPattern = /^([0-9a-fA-F]{2}[:-]?){5}[0-9a-fA-F]{2}$|^[0-9a-fA-F]{12}$|^[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}$/

const scheduleRules: FormRules = {
  name: [{ required: true, message: '请输入任务名称', trigger: 'blur' }],
  macAddress: [
    { required: true, message: '请输入 MAC 地址', trigger: 'blur' },
    { pattern: macPattern, message: 'MAC 地址格式不正确', trigger: 'blur' }
  ],
  broadcastAddress: [{ required: true, message: '请输入广播地址', trigger: 'blur' }],
  port: [{ required: true, type: 'number', message: '请输入端口', trigger: 'blur' }],
  scheduleMode: [{ required: true, message: '请选择执行方式', trigger: 'change' }],
  daysOfWeek: [
    {
      validator: (_rule, value, callback) => {
        if (scheduleForm.scheduleMode === 'weekly' && (!Array.isArray(value) || value.length === 0)) {
          callback(new Error('请选择每周执行的日期'))
          return
        }
        callback()
      },
      trigger: 'change'
    }
  ],
  specificDate: [
    {
      validator: (_rule, value, callback) => {
        if (scheduleForm.scheduleMode === 'date' && !value) {
          callback(new Error('请选择指定日期'))
          return
        }
        callback()
      },
      trigger: 'change'
    }
  ]
}

const weekDays = [
  { label: '周一', value: '1' },
  { label: '周二', value: '2' },
  { label: '周三', value: '3' },
  { label: '周四', value: '4' },
  { label: '周五', value: '5' },
  { label: '周六', value: '6' },
  { label: '周日', value: '0' }
]

async function loadLogs() {
  logLoading.value = true
  try {
    const res = await fetchWakeLogs()
    logs.value = res.data
  } finally {
    logLoading.value = false
  }
}

async function loadSchedules() {
  scheduleLoading.value = true
  try {
    const res = await fetchWakeSchedules()
    schedules.value = res.data
  } finally {
    scheduleLoading.value = false
  }
}

async function loadMacAddresses() {
  const res = await fetchWakeMacAddresses()
  macAddresses.value = res.data
}

async function loadAll() {
  await Promise.all([loadLogs(), loadSchedules(), loadMacAddresses()])
}

function openScheduleDialog(row?: WakeSchedule) {
  editingScheduleId.value = row?.id ?? null
  scheduleForm.name = row?.name ?? ''
  scheduleForm.macAddress = row?.macAddress ?? ''
  scheduleForm.broadcastAddress = row?.broadcastAddress ?? '255.255.255.255'
  scheduleForm.port = row?.port ?? 9
  scheduleForm.scheduleMode = row?.scheduleMode ?? 'daily'
  scheduleForm.daysOfWeek = row?.daysOfWeek ? row.daysOfWeek.split(',').filter(Boolean) : []
  scheduleForm.specificDate = row?.specificDate ? row.specificDate.slice(0, 10) : ''
  scheduleForm.isEnabled = row?.isEnabled ?? true
  scheduleTime.value = row?.timeOfDay ?? '08:00'
  scheduleDialogVisible.value = true
  scheduleFormRef.value?.clearValidate()
}

async function saveSchedule() {
  if (!await scheduleFormRef.value?.validate().catch(() => false)) return

  savingSchedule.value = true
  try {
    const data = {
      ...scheduleForm,
      daysOfWeek: scheduleForm.scheduleMode === 'weekly' ? scheduleForm.daysOfWeek.join(',') : '',
      specificDate: scheduleForm.scheduleMode === 'date' ? scheduleForm.specificDate : null,
      timeOfDay: scheduleTime.value
    }
    if (editingScheduleId.value) {
      await updateWakeSchedule(editingScheduleId.value, data)
      ElMessage.success('定时任务已更新')
    } else {
      await createWakeSchedule(data)
      ElMessage.success('定时任务已创建')
    }
    scheduleDialogVisible.value = false
    await Promise.all([loadSchedules(), loadMacAddresses()])
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '保存失败'
    ElMessage.error(msg)
  } finally {
    savingSchedule.value = false
  }
}

async function removeSchedule(id: number) {
  await ElMessageBox.confirm('确定要删除这个定时唤醒任务吗？', '提示', { type: 'warning' })
  await deleteWakeSchedule(id)
  ElMessage.success('定时任务已删除')
  await loadSchedules()
}

async function wakeLog(id: number) {
  wakingId.value = `log-${id}`
  try {
    const res = await wakeFromLog(id)
    ElMessage.success(res.data?.message ?? '魔术数据包已发送')
    await loadLogs()
  } finally {
    wakingId.value = ''
  }
}

async function wakeSchedule(id: number) {
  wakingId.value = `schedule-${id}`
  try {
    const res = await wakeFromSchedule(id)
    ElMessage.success(res.data?.message ?? '魔术数据包已发送')
    await loadLogs()
  } finally {
    wakingId.value = ''
  }
}

function macOptionLabel(item: WakeMacAddress) {
  return item.name === item.macAddress
    ? `${item.macAddress}（未命名）`
    : `${item.name}（${item.macAddress}）`
}

function formatScheduleRule(row: WakeSchedule) {
  if (row.scheduleMode === 'date') {
    return row.specificDate ? `指定日期 ${row.specificDate.slice(0, 10)}` : '指定日期'
  }

  if (row.scheduleMode === 'weekly') {
    const labels = row.daysOfWeek
      .split(',')
      .filter(Boolean)
      .map(value => weekDays.find(day => day.value === value)?.label ?? value)
    return labels.length > 0 ? labels.join('、') : '每周'
  }

  return '每天'
}

onMounted(loadAll)
</script>

<style scoped>
.wake-records-page {
  max-width: 1400px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
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

.section-card {
  margin-bottom: 18px;
  border-radius: 8px;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

@media (max-width: 768px) {
  .wake-records-page {
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
