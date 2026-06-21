<template>
  <div class="settings-page">
    <h2>系统设置</h2>

    <el-row :gutter="24">
      <!-- frpc Server Config -->
      <el-col :xs="24" :lg="14">
        <el-card class="settings-card">
          <template #header>
            <div class="card-header">
              <el-icon><Setting /></el-icon>
              <span>frpc 服务器配置</span>
            </div>
          </template>

          <el-form
            ref="configFormRef"
            :model="configForm"
            :rules="configRules"
            label-width="140px"
            v-loading="configLoading"
          >
            <el-form-item label="服务器地址" prop="serverAddr">
              <el-input v-model="configForm.serverAddr" placeholder="例如：soft.mybips.com" />
              <div class="form-hint">frp 服务端的域名或 IP 地址</div>
            </el-form-item>
            <el-form-item label="服务器端口" prop="serverPort">
              <el-input-number
                v-model="configForm.serverPort"
                :min="1" :max="65535"
                style="width: 100%"
              />
              <div class="form-hint">frp 服务端监听的端口，默认 7000</div>
            </el-form-item>
            <el-form-item label="认证方式" prop="authMethod">
              <el-select v-model="configForm.authMethod" style="width: 100%">
                <el-option label="Token 认证" value="token" />
                <el-option label="无认证" value="" />
              </el-select>
            </el-form-item>
            <el-form-item label="认证 Token" prop="authToken">
              <el-input
                v-model="configForm.authToken"
                type="password"
                show-password
                placeholder="与服务端配置保持一致"
              />
              <div class="form-hint">frp 服务端设置的 token，需与服务端一致</div>
            </el-form-item>
            <el-divider content-position="left" style="font-size: 13px">Web 管理界面</el-divider>
            <el-form-item label="监听地址" prop="webServerAddr">
              <el-input v-model="configForm.webServerAddr" placeholder="127.0.0.1" />
              <div class="form-hint">frpc 内置 Web 服务的监听地址</div>
            </el-form-item>
            <el-form-item label="监听端口" prop="webServerPort">
              <el-input-number
                v-model="configForm.webServerPort"
                :min="1" :max="65535"
                style="width: 100%"
              />
              <div class="form-hint">frpc 内置 Web 服务的监听端口，默认 7400</div>
            </el-form-item>
            <el-form-item>
              <el-button
                type="primary"
                :loading="configSaving"
                @click="saveConfig"
                :icon="Check"
              >
                保存并重新加载
              </el-button>
              <el-button @click="reloadFrpcNow" :loading="reloading" :icon="Refresh">
                仅重新加载
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card class="settings-card">
          <template #header>
            <div class="card-header">
              <el-icon><DataLine /></el-icon>
              <span>健康检查</span>
            </div>
          </template>
          <el-descriptions :column="1" v-loading="healthLoading">
            <el-descriptions-item label="服务状态">
              <el-tag :type="health?.status === 'healthy' ? 'success' : 'danger'">
                {{ health?.status ?? 'unknown' }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="数据库">{{ health?.database ?? '-' }}</el-descriptions-item>
            <el-descriptions-item label="frpc">{{ health?.frpc ?? '-' }}</el-descriptions-item>
          </el-descriptions>
          <el-button style="margin-top: 12px" :loading="healthLoading" @click="loadHealth">
            刷新健康状态
          </el-button>
        </el-card>

        <el-card class="settings-card">
          <template #header>
            <div class="card-header">
              <el-icon><Files /></el-icon>
              <span>配置备份与恢复</span>
            </div>
          </template>
          <div class="backup-actions">
            <el-button type="primary" :loading="backupLoading" @click="downloadBackup">
              导出配置备份
            </el-button>
            <el-button :loading="restoreLoading" @click="backupInputRef?.click()">
              导入并恢复
            </el-button>
            <input
              ref="backupInputRef"
              type="file"
              accept="application/json,.json"
              class="hidden-input"
              @change="restoreFromFile"
            />
          </div>
          <div class="form-hint">备份包含通道列表、HTTPS 代理规则和 frpc 配置，不包含用户密码、上传的证书文件、私钥和证书密码。</div>
        </el-card>
      </el-col>

      <!-- Password & Info -->
      <el-col :xs="24" :lg="10">
        <el-card class="settings-card" style="margin-bottom: 20px">
          <template #header>
            <div class="card-header">
              <el-icon><Lock /></el-icon>
              <span>账号安全</span>
            </div>
          </template>

          <el-form
            ref="pwdFormRef"
            :model="pwdForm"
            :rules="pwdRules"
            label-width="100px"
          >
            <el-form-item label="当前密码" prop="currentPassword">
              <el-input v-model="pwdForm.currentPassword" type="password" show-password />
            </el-form-item>
            <el-form-item label="新密码" prop="newPassword">
              <el-input v-model="pwdForm.newPassword" type="password" show-password />
            </el-form-item>
            <el-form-item label="确认新密码" prop="confirmPassword">
              <el-input v-model="pwdForm.confirmPassword" type="password" show-password />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="pwdSaving" @click="changePassword">
                修改密码
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card class="settings-card">
          <template #header>
            <div class="card-header">
              <el-icon><InfoFilled /></el-icon>
              <span>关于</span>
            </div>
          </template>
          <el-descriptions :column="1">
            <el-descriptions-item label="版本">v1.2.0</el-descriptions-item>
            <el-descriptions-item label="项目地址">
              <el-link
                type="primary"
                href="https://github.com/fzj929/frpcManager"
                target="_blank"
              >
                fzj929/frpcManager
              </el-link>
            </el-descriptions-item>
            <el-descriptions-item label="后端">ASP.NET Core 8.0</el-descriptions-item>
            <el-descriptions-item label="前端">Vue 3 + Element Plus</el-descriptions-item>
            <el-descriptions-item label="数据库">SQLite</el-descriptions-item>
            <el-descriptions-item label="frpc API">
              <el-link
                type="primary"
                href="http://127.0.0.1:7400"
                target="_blank"
              >
                http://127.0.0.1:7400
              </el-link>
            </el-descriptions-item>
          </el-descriptions>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Setting, Lock, InfoFilled, Check, Refresh, DataLine, Files } from '@element-plus/icons-vue'
import { fetchConfig, saveConfig as apiSaveConfig, reloadFrpc as apiReload, authChangePassword, exportBackup, restoreBackup, fetchHealth } from '@/api'
import type { FrpcConfig, HealthStatus } from '@/types'

// Config form
const configFormRef = ref<FormInstance>()
const configLoading = ref(false)
const configSaving = ref(false)
const reloading = ref(false)
const healthLoading = ref(false)
const backupLoading = ref(false)
const restoreLoading = ref(false)
const health = ref<HealthStatus | null>(null)
const backupInputRef = ref<HTMLInputElement>()

const configForm = reactive<FrpcConfig>({
  serverAddr: '',
  serverPort: 7000,
  authMethod: 'token',
  authToken: '',
  webServerAddr: '127.0.0.1',
  webServerPort: 7400
})

const configRules: FormRules = {
  serverAddr: [{ required: true, message: '请输入服务器地址', trigger: 'blur' }],
  serverPort: [{ required: true, type: 'number', message: '请输入服务器端口', trigger: 'blur' }]
}

async function loadConfig() {
  configLoading.value = true
  try {
    const res = await fetchConfig()
    Object.assign(configForm, res.data)
  } catch {
    ElMessage.warning('无法从 frpc 获取配置，frpc 可能未运行')
  } finally {
    configLoading.value = false
  }
}

async function saveConfig() {
  if (!await configFormRef.value?.validate().catch(() => false)) return
  configSaving.value = true
  try {
    await apiSaveConfig(configForm)
    ElMessage.success('配置已保存并重新加载')
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })
      ?.response?.data?.message ?? '保存失败'
    ElMessage.error(msg)
  } finally {
    configSaving.value = false
  }
}

async function reloadFrpcNow() {
  reloading.value = true
  try {
    await apiReload()
    ElMessage.success('frpc 已重新加载')
  } catch {
    ElMessage.error('重新加载失败')
  } finally {
    reloading.value = false
  }
}

async function loadHealth() {
  healthLoading.value = true
  try {
    const res = await fetchHealth()
    health.value = res.data
  } catch (err: unknown) {
    health.value = (err as { response?: { data?: HealthStatus } })?.response?.data ?? null
    ElMessage.error('健康检查失败')
  } finally {
    healthLoading.value = false
  }
}

async function downloadBackup() {
  backupLoading.value = true
  try {
    const res = await exportBackup()
    const blob = new Blob([JSON.stringify(res.data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `frpcmanager-backup-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
    link.click()
    URL.revokeObjectURL(url)
    ElMessage.success('配置备份已导出')
  } catch {
    ElMessage.error('导出配置备份失败')
  } finally {
    backupLoading.value = false
  }
}

async function restoreFromFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  try {
    await ElMessageBox.confirm('恢复配置会覆盖现有通道和 HTTPS 代理规则，并可能写入 frpc 配置。确定继续吗？', '恢复确认', {
      type: 'warning',
      confirmButtonText: '恢复',
      cancelButtonText: '取消'
    })

    restoreLoading.value = true
    const text = await file.text()
    const backup = JSON.parse(text)
    await restoreBackup({
      proxies: backup.proxies ?? [],
      httpsProxies: backup.httpsProxies ?? [],
      frpcConfig: backup.frpcConfig ?? null,
      replaceExisting: true,
      applyFrpcConfig: true
    })
    ElMessage.success('配置已恢复')
  } catch (err: unknown) {
    if (err !== 'cancel') ElMessage.error('恢复配置失败')
  } finally {
    restoreLoading.value = false
    input.value = ''
  }
}

// Password form
const pwdFormRef = ref<FormInstance>()
const pwdSaving = ref(false)
const pwdForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const pwdRules: FormRules = {
  currentPassword: [{ required: true, message: '请输入当前密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码不能少于 6 个字符', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (_rule, value, callback) => {
        if (value !== pwdForm.newPassword) callback(new Error('两次输入的密码不一致'))
        else callback()
      },
      trigger: 'blur'
    }
  ]
}

async function changePassword() {
  if (!await pwdFormRef.value?.validate().catch(() => false)) return
  pwdSaving.value = true
  try {
    await authChangePassword(pwdForm.currentPassword, pwdForm.newPassword)
    ElMessage.success('密码修改成功')
    pwdForm.currentPassword = ''
    pwdForm.newPassword = ''
    pwdForm.confirmPassword = ''
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })
      ?.response?.data?.message ?? '密码修改失败'
    ElMessage.error(msg)
  } finally {
    pwdSaving.value = false
  }
}

onMounted(() => {
  loadConfig()
  loadHealth()
})
</script>

<style scoped>
.settings-page h2 {
  font-size: 22px;
  font-weight: 600;
  color: #1a1a2e;
  margin-bottom: 20px;
}

.settings-card {
  border-radius: 12px;
  margin-bottom: 20px;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

.form-hint {
  font-size: 12px;
  color: #aaa;
  margin-top: 4px;
}

.backup-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.hidden-input {
  display: none;
}

@media (max-width: 768px) {
  .settings-page h2 {
    font-size: 20px;
    margin-bottom: 14px;
  }

  .settings-card {
    margin-bottom: 14px;
  }

  .settings-card :deep(.el-form-item__label) {
    width: 100% !important;
    justify-content: flex-start;
    margin-bottom: 6px;
  }

  .settings-card :deep(.el-form-item) {
    display: block;
  }

  .settings-card :deep(.el-form-item__content) {
    margin-left: 0 !important;
  }

  .settings-card :deep(.el-button) {
    width: 100%;
    margin-left: 0;
    margin-bottom: 8px;
  }
}
</style>
