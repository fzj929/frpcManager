<template>
  <div class="proxies-page">
    <div class="page-header">
      <h2>通道管理</h2>
      <div class="header-actions">
        <el-input
          v-model="searchText"
          placeholder="搜索通道名称..."
          clearable
          style="width: 200px"
          :prefix-icon="Search"
        />
        <el-select v-model="filterType" placeholder="协议类型" clearable style="width: 120px">
          <el-option label="全部类型" value="" />
          <el-option label="TCP" value="tcp" />
          <el-option label="UDP" value="udp" />
        </el-select>
        <el-select v-model="filterStatus" placeholder="运行状态" clearable style="width: 120px">
          <el-option label="全部状态" value="" />
          <el-option label="运行中" value="running" />
          <el-option label="已停用" value="disabled" />
        </el-select>
        <el-button @click="handleSync" :loading="syncing" :icon="Refresh">从 frpc 同步</el-button>
        <el-button type="primary" @click="openAddDialog" :icon="Plus">添加通道</el-button>
      </div>
    </div>

    <el-card class="table-card">
      <el-table
        :data="filteredProxies"
        v-loading="loading"
        row-key="id"
        stripe
        highlight-current-row
      >
        <!-- Status dot -->
        <el-table-column label="状态" width="72" align="center">
          <template #default="{ row }">
            <el-tooltip :content="statusText(row.status)" placement="top">
              <span :class="['status-dot', `status-${row.status}`]"></span>
            </el-tooltip>
          </template>
        </el-table-column>

        <!-- Name + countdown -->
        <el-table-column label="通道名称" min-width="180">
          <template #default="{ row }">
            <span class="proxy-name">{{ row.name }}</span>
            <div v-if="row.description" class="proxy-desc">{{ row.description }}</div>
            <!-- Countdown badge -->
            <div v-if="row.isEnabled && row.expiresAt" class="countdown-badge">
              <el-icon size="12"><Timer /></el-icon>
              <span :class="isUrgent(row.expiresAt) ? 'urgent' : ''">
                {{ countdown(row.expiresAt) }}
              </span>
            </div>
            <div v-else-if="row.isEnabled && !row.expiresAt" class="unlimited-badge">
              <el-icon size="12"><Unlock /></el-icon>
              <span>无时限</span>
            </div>
          </template>
        </el-table-column>

        <!-- Type -->
        <el-table-column label="类型" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="row.type === 'tcp' ? 'primary' : 'warning'" size="small" round>
              {{ row.type.toUpperCase() }}
            </el-tag>
          </template>
        </el-table-column>

        <!-- Local addr -->
        <el-table-column label="本地地址" min-width="160">
          <template #default="{ row }">
            <span class="addr-text">{{ row.localIP }}:{{ row.localPort }}</span>
          </template>
        </el-table-column>

        <!-- Remote port -->
        <el-table-column label="远程端口" width="100" align="center">
          <template #default="{ row }">
            <el-tag type="info" size="small">:{{ row.remotePort }}</el-tag>
          </template>
        </el-table-column>

        <!-- Remote addr -->
        <el-table-column label="远程地址" min-width="180">
          <template #default="{ row }">
            <span v-if="row.remoteAddr" class="addr-text muted">{{ row.remoteAddr }}</span>
            <span v-else class="muted">—</span>
          </template>
        </el-table-column>

        <!-- Enable toggle -->
        <el-table-column label="启用" width="90" align="center">
          <template #default="{ row }">
            <el-switch
              :model-value="row.isEnabled"
              :loading="togglingId === row.id"
              @change="(val: boolean) => onToggle(row, val)"
            />
          </template>
        </el-table-column>

        <!-- Actions -->
        <el-table-column label="操作" width="100" align="center" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" size="small" @click="openEditDialog(row)">
              <el-icon><Edit /></el-icon>
            </el-button>
            <el-button text type="danger" size="small" @click="handleDelete(row)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="table-footer">
        共 {{ filteredProxies.length }} 个通道，{{ activeCount }} 个运行中
      </div>
    </el-card>

    <!-- Proxy add/edit dialog -->
    <ProxyFormDialog
      v-model="dialogVisible"
      :proxy="editingProxy"
      @saved="handleSaved"
    />

    <!-- Timed enable dialog -->
    <TimedEnableDialog
      v-model="timedDialogVisible"
      :proxy-name="timedTarget?.name ?? ''"
      :proxy-type="timedTarget?.type ?? 'tcp'"
      :loading="togglingId === timedTarget?.id"
      @confirm="handleTimedConfirm"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Plus, Refresh, Edit, Delete, Timer, Unlock } from '@element-plus/icons-vue'
import { fetchProxies, enableProxy, disableProxy, deleteProxy, syncFromFrpc } from '@/api'
import type { Proxy } from '@/types'
import ProxyFormDialog from '@/components/ProxyFormDialog.vue'
import TimedEnableDialog from '@/components/TimedEnableDialog.vue'

const proxies = ref<Proxy[]>([])
const loading = ref(false)
const syncing = ref(false)
const togglingId = ref<number | null>(null)
const searchText = ref('')
const filterType = ref('')
const filterStatus = ref('')
const dialogVisible = ref(false)
const editingProxy = ref<Proxy | null>(null)

// Timed enable
const timedDialogVisible = ref(false)
const timedTarget = ref<Proxy | null>(null)

// Countdown ticker
const tick = ref(0)
let ticker: ReturnType<typeof setInterval> | null = null

const filteredProxies = computed(() => {
  return proxies.value.filter(p => {
    if (searchText.value && !p.name.toLowerCase().includes(searchText.value.toLowerCase())) return false
    if (filterType.value && p.type !== filterType.value) return false
    if (filterStatus.value === 'running' && p.status !== 'running') return false
    if (filterStatus.value === 'disabled' && p.isEnabled) return false
    return true
  })
})

const activeCount = computed(() => proxies.value.filter(p => p.status === 'running').length)

function statusText(status: string): string {
  const map: Record<string, string> = { running: '运行中', disabled: '已停用', error: '错误', unknown: '未知' }
  return map[status] ?? status
}

// Countdown - tick changes every second to force reactivity
function countdown(expiresAt: string | null): string {
  void tick.value  // depend on tick for reactivity
  if (!expiresAt) return ''
  const expiry = new Date(expiresAt.endsWith('Z') ? expiresAt : expiresAt + 'Z')
  const diff = expiry.getTime() - Date.now()
  if (diff <= 0) return '即将关闭'
  const h = Math.floor(diff / 3600000)
  const m = Math.floor((diff % 3600000) / 60000)
  const s = Math.floor((diff % 60000) / 1000)
  if (h > 0) return `${h}h ${m}m 后自动关闭`
  if (m > 0) return `${m}m ${s}s 后自动关闭`
  return `${s}s 后自动关闭`
}

function isUrgent(expiresAt: string | null): boolean {
  if (!expiresAt) return false
  const expiry = new Date(expiresAt.endsWith('Z') ? expiresAt : expiresAt + 'Z')
  return expiry.getTime() - Date.now() < 5 * 60 * 1000  // < 5 min
}

// Toggle handler
function onToggle(row: Proxy, enable: boolean) {
  if (!enable) {
    // Disable immediately, no dialog needed
    doDisable(row)
  } else {
    // Show timed enable dialog
    timedTarget.value = row
    timedDialogVisible.value = true
  }
}

async function handleTimedConfirm(durationMinutes: number | null) {
  if (!timedTarget.value) return
  const row = timedTarget.value
  togglingId.value = row.id
  try {
    await enableProxy(row.id, durationMinutes)
    const msg = durationMinutes
      ? `通道 "${row.name}" 已启用，${formatDuration(durationMinutes)}后自动关闭`
      : `通道 "${row.name}" 已启用（无时间限制）`
    ElMessage.success(msg)
    timedDialogVisible.value = false
    await loadProxies()
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })
      ?.response?.data?.message ?? '启用失败'
    ElMessage.error(msg)
  } finally {
    togglingId.value = null
  }
}

async function doDisable(row: Proxy) {
  togglingId.value = row.id
  try {
    await disableProxy(row.id)
    ElMessage.success(`通道 "${row.name}" 已停用`)
    await loadProxies()
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })
      ?.response?.data?.message ?? '停用失败'
    ElMessage.error(msg)
  } finally {
    togglingId.value = null
  }
}

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} 分钟`
  return `${minutes / 60} 小时`
}

async function handleDelete(row: Proxy) {
  await ElMessageBox.confirm(
    `确定要删除通道 "${row.name}" 吗？${row.isEnabled ? '该通道当前已启用，删除后将从 frpc 配置中移除。' : ''}`,
    '删除确认',
    { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger' }
  )
  try {
    await deleteProxy(row.id)
    ElMessage.success('删除成功')
    await loadProxies()
  } catch {
    ElMessage.error('删除失败')
  }
}

async function handleSync() {
  syncing.value = true
  try {
    await syncFromFrpc()
    ElMessage.success('已从 frpc 同步通道配置')
    await loadProxies()
  } catch {
    ElMessage.error('同步失败，请检查 frpc 是否运行')
  } finally {
    syncing.value = false
  }
}

function openAddDialog() {
  editingProxy.value = null
  dialogVisible.value = true
}

function openEditDialog(proxy: Proxy) {
  editingProxy.value = proxy
  dialogVisible.value = true
}

function handleSaved() {
  dialogVisible.value = false
  loadProxies()
}

async function loadProxies() {
  loading.value = true
  try {
    const res = await fetchProxies()
    proxies.value = res.data
  } catch {
    ElMessage.error('加载通道列表失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadProxies()
  // Update countdown every second
  ticker = setInterval(() => { tick.value++ }, 1000)
  // Auto-refresh list every 30s to pick up server-side expiry
  setInterval(loadProxies, 30000)
})

onUnmounted(() => {
  if (ticker) clearInterval(ticker)
})
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 12px;
}

.page-header h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  color: #1a1a2e;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.table-card { border-radius: 12px; }

.status-dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.status-running { background-color: #67C23A; box-shadow: 0 0 6px rgba(103,194,58,.6); }
.status-disabled { background-color: #C0C4CC; }
.status-error { background-color: #F56C6C; }
.status-unknown { background-color: #E6A23C; }

.proxy-name { font-weight: 500; font-size: 14px; }
.proxy-desc { font-size: 12px; color: #999; margin-top: 2px; }

.countdown-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-top: 4px;
  font-size: 11px;
  color: #E6A23C;
  background: #fdf6ec;
  padding: 2px 7px;
  border-radius: 10px;
  border: 1px solid #f5dab1;
}

.countdown-badge .urgent { color: #F56C6C; font-weight: 600; }

.unlimited-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-top: 4px;
  font-size: 11px;
  color: #909399;
  background: #f5f5f5;
  padding: 2px 7px;
  border-radius: 10px;
}

.addr-text { font-family: 'Courier New', monospace; font-size: 13px; }
.muted { color: #aaa; }

.table-footer {
  padding: 12px 0 0;
  font-size: 13px;
  color: #888;
  text-align: right;
}

@media (max-width: 768px) {
  .page-header {
    align-items: stretch;
  }

  .page-header h2 {
    font-size: 20px;
  }

  .header-actions {
    width: 100%;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
  }

  .header-actions :deep(.el-input),
  .header-actions :deep(.el-select),
  .header-actions :deep(.el-button) {
    width: 100% !important;
  }

  .header-actions :deep(.el-input) {
    grid-column: 1 / -1;
  }

  .table-card :deep(.el-card__body) {
    overflow-x: auto;
  }

  .table-card :deep(.el-table) {
    min-width: 760px;
  }

  .table-footer {
    text-align: left;
    white-space: normal;
  }
}
</style>
