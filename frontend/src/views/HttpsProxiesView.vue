<template>
  <div class="https-proxy-page">
    <div class="page-header">
      <h2>HTTPS 代理</h2>
      <el-button type="primary" :icon="Plus" @click="openDialog()">新增代理</el-button>
    </div>

    <el-alert
      class="page-alert"
      type="info"
      :closable="false"
      show-icon
      title="轻量 HTTPS 反向代理会在本机监听 HTTPS 端口，并转发到内网 HTTP 服务。证书可使用网站默认证书，也可以上传 IIS 证书（.pfx/.p12）或 Nginx 证书（.pem/.crt/.cer + .key）。"
    />

    <el-card class="table-card">
      <el-table
        :data="rules"
        v-loading="loading"
        row-key="id"
        stripe
        :row-class-name="tableRowClassName"
      >
        <el-table-column prop="name" label="名称" min-width="140" />
        <el-table-column label="访问地址" min-width="170">
          <template #default="{ row }">
            <el-link type="primary" :href="`https://${hostName}:${row.listenPort}`" target="_blank">
              https://{{ hostName }}:{{ row.listenPort }}
            </el-link>
          </template>
        </el-table-column>
        <el-table-column prop="targetUrl" label="目标 HTTP 地址" min-width="220" show-overflow-tooltip />
        <el-table-column label="创建者" width="180">
          <template #default="{ row }">
            <el-select
              v-if="auth.isAdmin"
              :model-value="row.createdByUserId"
              :loading="ownerSavingId === row.id"
              size="small"
              clearable
              placeholder="未分配"
              style="width: 100%"
              @change="(value: number | undefined) => handleOwnerChange(row, value ?? null)"
            >
              <el-option
                v-for="user in ownerUsers"
                :key="user.id"
                :label="`${user.username}${user.isDisabled ? '（禁用）' : ''}`"
                :value="user.id"
              />
            </el-select>
            <span v-else class="muted">{{ row.createdByUsername || '历史配置' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="证书" width="120">
          <template #default="{ row }">
            <el-tag :type="row.certificateMode === 'default' ? 'info' : 'warning'">
              {{ certificateModeLabel(row.certificateMode) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column v-if="!auth.isAdmin" label="权限" width="90" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.canManage" type="success" size="small" effect="plain">可操作</el-tag>
            <el-tooltip v-else content="只能查看，不能修改或删除其他用户的 HTTPS 代理" placement="top">
              <el-tag type="info" size="small" effect="plain">只读</el-tag>
            </el-tooltip>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tooltip
              :disabled="row.canManage"
              content="只能查看，不能启停其他用户的 HTTPS 代理"
              placement="top"
            >
              <span class="switch-wrap">
                <el-switch
                  v-model="row.isEnabled"
                  :loading="switchingId === row.id"
                  :disabled="!row.canManage"
                  @change="toggleRule(row)"
                />
              </span>
            </el-tooltip>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="备注" min-width="160" show-overflow-tooltip />
        <el-table-column label="操作" width="170" fixed="right">
          <template #default="{ row }">
            <div class="row-actions">
              <el-tooltip
                :disabled="row.canManage"
                content="只能查看，不能编辑其他用户的 HTTPS 代理"
                placement="top"
              >
                <span>
                  <el-button size="small" :icon="Edit" :disabled="!row.canManage" @click="openDialog(row)">编辑</el-button>
                </span>
              </el-tooltip>
              <el-tooltip
                :disabled="row.canManage"
                content="只能查看，不能删除其他用户的 HTTPS 代理"
                placement="top"
              >
                <span>
                  <el-button size="small" type="danger" :icon="Delete" :disabled="!row.canManage" @click="removeRule(row)">删除</el-button>
                </span>
              </el-tooltip>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 HTTPS 代理' : '新增 HTTPS 代理'" width="620px">
      <el-form ref="formRef" :model="form" :rules="rulesDef" label-width="120px">
        <el-form-item label="名称" prop="name">
          <el-input v-model.trim="form.name" placeholder="例如：内网管理后台" />
        </el-form-item>
        <el-form-item label="监听端口" prop="listenPort">
          <el-input-number v-model="form.listenPort" :min="1" :max="65535" style="width: 100%" />
          <div class="form-hint">用户访问此端口的 HTTPS 地址，例如 8443。</div>
        </el-form-item>
        <el-form-item label="目标地址" prop="targetUrl">
          <el-input v-model.trim="form.targetUrl" placeholder="http://192.168.1.10:8080" />
          <div class="form-hint">第一版仅支持转发到 HTTP 地址。</div>
        </el-form-item>
        <template v-if="!editingId">
          <el-form-item label="frp 通道">
            <el-checkbox v-model="form.createFrpChannel">同时创建 frp 通道</el-checkbox>
            <div class="form-hint">
              通道会默认停用。创建后请到通道管理中打开通道，外网才能通过 frp 访问该 HTTPS 代理。
            </div>
          </el-form-item>
          <el-form-item v-if="form.createFrpChannel" label="通道名称" prop="frpChannelName">
            <el-input
              v-model.trim="form.frpChannelName"
              placeholder="例如：https-office"
              maxlength="64"
              show-word-limit
            />
            <div class="form-hint">
              规则同通道管理：只能包含字母、数字、下划线和连字符。本地 IP 为 127.0.0.1，本地端口和远程端口都使用 HTTPS 监听端口。
            </div>
          </el-form-item>
        </template>
        <el-form-item label="证书来源">
          <el-radio-group v-model="form.certificateMode">
            <el-radio-button label="default">默认证书</el-radio-button>
            <el-radio-button label="pfx">IIS证书</el-radio-button>
            <el-radio-button label="pem">Nginx证书</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <template v-if="form.certificateMode === 'pfx'">
          <el-form-item label="IIS证书">
            <el-upload
              :auto-upload="false"
              :limit="1"
              accept=".pfx,.p12"
              :on-change="onCertChange"
              :on-remove="onCertRemove"
            >
              <el-button :icon="Upload">选择 .pfx/.p12</el-button>
            </el-upload>
            <div class="form-hint">适用于 Windows IIS 导出的 PFX/P12 证书，通常包含证书和私钥。</div>
          </el-form-item>
          <el-form-item label="证书密码">
            <el-input v-model="form.certificatePassword" type="password" show-password placeholder="PFX 证书密码" />
          </el-form-item>
        </template>
        <template v-if="form.certificateMode === 'pem'">
          <el-form-item label="Nginx证书">
            <el-upload
              :auto-upload="false"
              :limit="1"
              accept=".pem,.crt,.cer"
              :on-change="onCertChange"
              :on-remove="onCertRemove"
            >
              <el-button :icon="Upload">选择证书文件</el-button>
            </el-upload>
            <div class="form-hint">适用于 Nginx/Caddy/Let's Encrypt 的 fullchain.pem、cert.pem、.crt 或 .cer。</div>
          </el-form-item>
          <el-form-item label="私钥文件">
            <el-upload
              :auto-upload="false"
              :limit="1"
              accept=".key"
              :on-change="onKeyChange"
              :on-remove="onKeyRemove"
            >
              <el-button :icon="Upload">选择 .key 私钥</el-button>
            </el-upload>
          </el-form-item>
          <el-form-item label="私钥密码">
            <el-input v-model="form.certificatePassword" type="password" show-password placeholder="没有密码可留空" />
          </el-form-item>
        </template>
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model.trim="form.description" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="saveRule">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules, type UploadFile } from 'element-plus'
import { Delete, Edit, Plus, Upload } from '@element-plus/icons-vue'
import {
  assignHttpsProxyOwner,
  createHttpsProxy,
  deleteHttpsProxy,
  disableHttpsProxy,
  enableHttpsProxy,
  fetchHttpsProxies,
  fetchUsers,
  updateHttpsProxy
} from '@/api'
import type { HttpsProxyRule, UserAccount } from '@/types'
import { useAuthStore } from '@/stores/auth'

const hostName = window.location.hostname || 'localhost'
const proxyRules = ref<HttpsProxyRule[]>([])
const rules = proxyRules
const auth = useAuthStore()
const loading = ref(false)
const saving = ref(false)
const switchingId = ref<number | null>(null)
const ownerSavingId = ref<number | null>(null)
const ownerUsers = ref<UserAccount[]>([])
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const certFile = ref<File | null>(null)
const keyFile = ref<File | null>(null)

const form = reactive({
  name: '',
  listenPort: 8443,
  targetUrl: '',
  certificateMode: 'default',
  certificatePassword: '',
  description: '',
  isEnabled: true,
  createFrpChannel: false,
  frpChannelName: ''
})

const rulesDef: FormRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  listenPort: [{ required: true, type: 'number', message: '请输入监听端口', trigger: 'blur' }],
  targetUrl: [
    { required: true, message: '请输入目标地址', trigger: 'blur' },
    {
      validator: (_rule, value, callback) => {
        if (!/^http:\/\/.+/i.test(value)) callback(new Error('目标地址必须是 HTTP URL'))
        else callback()
      },
      trigger: 'blur'
    }
  ],
  frpChannelName: [
    {
      validator: (_rule, value, callback) => {
        if (editingId.value || !form.createFrpChannel) {
          callback()
          return
        }

        if (!value) {
          callback(new Error('请输入通道名称'))
          return
        }

        if (!/^[a-zA-Z0-9_-]+$/.test(value)) {
          callback(new Error('只能包含字母、数字、下划线和连字符'))
          return
        }

        callback()
      },
      trigger: 'blur'
    }
  ]
}

function tableRowClassName({ row }: { row: HttpsProxyRule }) {
  if (auth.isAdmin || row.canManage) return ''
  return 'readonly-row'
}

async function loadRules() {
  loading.value = true
  try {
    const res = await fetchHttpsProxies()
    proxyRules.value = res.data
  } finally {
    loading.value = false
  }
}

function openDialog(row?: HttpsProxyRule) {
  editingId.value = row?.id ?? null
  form.name = row?.name ?? ''
  form.listenPort = row?.listenPort ?? 8443
  form.targetUrl = row?.targetUrl ?? ''
  form.certificateMode = normalizeCertificateMode(row?.certificateMode)
  form.certificatePassword = ''
  form.description = row?.description ?? ''
  form.isEnabled = row?.isEnabled ?? true
  form.createFrpChannel = false
  form.frpChannelName = ''
  certFile.value = null
  keyFile.value = null
  dialogVisible.value = true
  formRef.value?.clearValidate()
}

function onCertChange(file: UploadFile) {
  certFile.value = file.raw ?? null
}

function onCertRemove() {
  certFile.value = null
}

function onKeyChange(file: UploadFile) {
  keyFile.value = file.raw ?? null
}

function onKeyRemove() {
  keyFile.value = null
}

function buildFormData() {
  const data = new FormData()
  data.append('name', form.name)
  data.append('listenPort', String(form.listenPort))
  data.append('targetUrl', form.targetUrl)
  data.append('certificateMode', form.certificateMode)
  data.append('certificatePassword', form.certificatePassword)
  data.append('description', form.description)
  data.append('isEnabled', String(form.isEnabled))
  data.append('createFrpChannel', String(!editingId.value && form.createFrpChannel))
  data.append('frpChannelName', form.frpChannelName)
  if (certFile.value) data.append('certificate', certFile.value)
  if (keyFile.value) data.append('privateKey', keyFile.value)
  return data
}

function certificateModeLabel(mode: string) {
  if (mode === 'pfx' || mode === 'uploaded') return 'IIS证书'
  if (mode === 'pem') return 'Nginx证书'
  return '默认证书'
}

function normalizeCertificateMode(mode?: string) {
  if (mode === 'pfx' || mode === 'uploaded') return 'pfx'
  if (mode === 'pem') return 'pem'
  return 'default'
}

async function saveRule() {
  if (!await formRef.value?.validate().catch(() => false)) return

  saving.value = true
  try {
    if (editingId.value) {
      await updateHttpsProxy(editingId.value, buildFormData())
      ElMessage.success('HTTPS 代理已更新')
    } else {
      await createHttpsProxy(buildFormData())
      ElMessage.success(form.createFrpChannel
        ? 'HTTPS 代理已创建，frp 通道已添加，请到通道管理中打开通道后再访问'
        : 'HTTPS 代理已创建')
    }
    dialogVisible.value = false
    await loadRules()
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '保存失败'
    ElMessage.error(msg)
  } finally {
    saving.value = false
  }
}

async function toggleRule(row: HttpsProxyRule) {
  switchingId.value = row.id
  try {
    if (row.isEnabled) await enableHttpsProxy(row.id)
    else await disableHttpsProxy(row.id)
    ElMessage.success(row.isEnabled ? '代理已启用' : '代理已停用')
  } catch (err: unknown) {
    row.isEnabled = !row.isEnabled
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '操作失败'
    ElMessage.error(msg)
  } finally {
    switchingId.value = null
  }
}

async function removeRule(row: HttpsProxyRule) {
  await ElMessageBox.confirm(`确定要删除 HTTPS 代理「${row.name}」吗？`, '提示', { type: 'warning' })
  await deleteHttpsProxy(row.id)
  ElMessage.success('HTTPS 代理已删除')
  await loadRules()
}

async function handleOwnerChange(row: HttpsProxyRule, userId: number | null) {
  ownerSavingId.value = row.id
  try {
    const res = await assignHttpsProxyOwner(row.id, userId)
    Object.assign(row, res.data)
    ElMessage.success('HTTPS 代理归属已更新')
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '更新 HTTPS 代理归属失败'
    ElMessage.error(msg)
    await loadRules()
  } finally {
    ownerSavingId.value = null
  }
}

async function loadOwnerUsers() {
  if (!auth.isAdmin) return

  try {
    const res = await fetchUsers()
    ownerUsers.value = res.data
  } catch {
    ElMessage.error('加载用户列表失败')
  }
}

onMounted(() => {
  loadRules()
  loadOwnerUsers()
})
</script>

<style scoped>
.https-proxy-page {
  max-width: 1400px;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  gap: 12px;
}

.page-header h2 {
  margin: 0;
  color: #303133;
}

.page-alert {
  margin-bottom: 16px;
}

.table-card {
  border-radius: 8px;
}

.table-card :deep(.readonly-row) {
  --el-table-tr-bg-color: #fafafa;
  color: #909399;
}

.table-card :deep(.readonly-row td:last-child) {
  background: #fafafa;
}

.row-actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.switch-wrap {
  display: inline-flex;
  align-items: center;
  min-width: 40px;
  justify-content: center;
}

.form-hint {
  margin-top: 6px;
  color: #909399;
  font-size: 12px;
  line-height: 1.4;
}

.muted {
  color: #909399;
}

@media (max-width: 768px) {
  .https-proxy-page {
    max-width: none;
  }

  .page-header {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
