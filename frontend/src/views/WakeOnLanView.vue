<template>
  <div class="wake-page">
    <div class="page-header">
      <h2>唤醒主机</h2>
    </div>

    <el-row :gutter="24">
      <el-col :xs="24" :lg="14">
        <el-card class="wake-card">
          <template #header>
            <div class="card-header">
              <el-icon><Monitor /></el-icon>
              <span>发送 Wake-on-LAN 魔术数据包</span>
            </div>
          </template>

          <el-form
            ref="formRef"
            :model="form"
            :rules="rules"
            label-width="120px"
            @submit.prevent
          >
            <el-form-item label="MAC 地址" prop="macAddress">
              <el-input
                v-model.trim="form.macAddress"
                placeholder="例如：00:11:22:33:44:55"
                clearable
              />
            </el-form-item>

            <el-form-item label="广播地址" prop="broadcastAddress">
              <el-input v-model.trim="form.broadcastAddress" placeholder="255.255.255.255" />
              <div class="form-hint">跨网段唤醒时通常需要填写目标网段广播地址，例如 192.168.1.255。</div>
            </el-form-item>

            <el-form-item label="端口" prop="port">
              <el-input-number v-model="form.port" :min="1" :max="65535" style="width: 100%" />
              <div class="form-hint">Wake-on-LAN 常用端口为 9，也有设备使用 7。</div>
            </el-form-item>

            <el-form-item>
              <el-button type="primary" :loading="sending" :icon="SwitchButton" @click="sendWakePacket">
                发送唤醒包
              </el-button>
              <el-button @click="resetForm">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="10">
        <el-card class="wake-card">
          <template #header>
            <div class="card-header">
              <el-icon><InfoFilled /></el-icon>
              <span>使用说明</span>
            </div>
          </template>

          <el-alert
            title="目标计算机需要在 BIOS/网卡/系统中开启 Wake-on-LAN，并且网线或电源保持可唤醒状态。"
            type="info"
            :closable="false"
            show-icon
          />

          <el-descriptions :column="1" class="tips">
            <el-descriptions-item label="默认广播">255.255.255.255</el-descriptions-item>
            <el-descriptions-item label="默认端口">9</el-descriptions-item>
            <el-descriptions-item label="支持格式">
              00:11:22:33:44:55、00-11-22-33-44-55、001122334455、0011.2233.4455
            </el-descriptions-item>
          </el-descriptions>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { InfoFilled, Monitor, SwitchButton } from '@element-plus/icons-vue'
import { wakeOnLan } from '@/api'
import type { WakeOnLanRequest } from '@/types'

const formRef = ref<FormInstance>()
const sending = ref(false)

const form = reactive<WakeOnLanRequest>({
  macAddress: '',
  broadcastAddress: '255.255.255.255',
  port: 9
})

const macPattern = /^([0-9a-fA-F]{2}[:-]?){5}[0-9a-fA-F]{2}$|^[0-9a-fA-F]{12}$|^[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}\.[0-9a-fA-F]{4}$/

const rules: FormRules = {
  macAddress: [
    { required: true, message: '请输入 MAC 地址', trigger: 'blur' },
    {
      pattern: macPattern,
      message: 'MAC 地址格式不正确',
      trigger: 'blur'
    }
  ],
  broadcastAddress: [{ required: true, message: '请输入广播地址', trigger: 'blur' }],
  port: [{ required: true, type: 'number', message: '请输入端口', trigger: 'blur' }]
}

async function sendWakePacket() {
  if (!await formRef.value?.validate().catch(() => false)) return

  sending.value = true
  try {
    const res = await wakeOnLan(form)
    ElMessage.success(res.data?.message ?? '魔术数据包已发送')
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })
      ?.response?.data?.message ?? '发送失败'
    ElMessage.error(msg)
  } finally {
    sending.value = false
  }
}

function resetForm() {
  form.macAddress = ''
  form.broadcastAddress = '255.255.255.255'
  form.port = 9
  formRef.value?.clearValidate()
}
</script>

<style scoped>
.wake-page {
  max-width: 1200px;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.page-header h2 {
  margin: 0;
  color: #303133;
}

.wake-card {
  border-radius: 8px;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

.form-hint {
  margin-top: 6px;
  color: #909399;
  font-size: 12px;
  line-height: 1.4;
}

.tips {
  margin-top: 18px;
}
</style>
