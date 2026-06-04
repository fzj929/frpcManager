<template>
  <el-dialog
    v-model="visible"
    :title="isEdit ? '编辑通道' : '添加通道'"
    width="560px"
    :close-on-click-modal="false"
    @closed="resetForm"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="100px"
    >
      <el-form-item label="通道名称" prop="name">
        <el-input v-model="form.name" placeholder="例如：rdp-office" maxlength="64" show-word-limit />
        <div class="field-hint">唯一标识符，用于区分不同通道，建议使用英文和数字</div>
      </el-form-item>

      <el-form-item label="协议类型" prop="type">
        <el-radio-group v-model="form.type">
          <el-radio-button value="tcp">TCP</el-radio-button>
          <el-radio-button value="udp">UDP</el-radio-button>
        </el-radio-group>
        <div class="field-hint">
          TCP：用于 RDP、SSH 等需要可靠传输的协议；UDP：用于游戏、语音等对延迟敏感的协议
        </div>
      </el-form-item>

      <el-form-item label="本地 IP" prop="localIP">
        <el-input v-model="form.localIP" placeholder="例如：192.168.0.100" />
        <div class="field-hint">需要暴露的内网机器 IP 地址（frpc 所在机器可访问的地址）</div>
      </el-form-item>

      <el-row :gutter="16">
        <el-col :span="12">
          <el-form-item label="本地端口" prop="localPort">
            <el-input-number
              v-model="form.localPort"
              :min="1" :max="65535"
              style="width: 100%"
              placeholder="例如：3389"
            />
            <div class="field-hint">内网机器上的服务端口（如 RDP=3389）</div>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="远程端口" prop="remotePort">
            <el-input-number
              v-model="form.remotePort"
              :min="1" :max="65535"
              style="width: 100%"
              placeholder="例如：6001"
            />
            <div class="field-hint">frp 服务器上开放的公网端口</div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="描述" prop="description">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="2"
          placeholder="可选，用于备注该通道的用途"
          maxlength="200"
          show-word-limit
        />
      </el-form-item>

      <el-alert
        v-if="!isEdit"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 8px"
      >
        <template #default>
          添加后通道默认为<strong>停用</strong>状态，请在列表中点击开关启用。启用时将自动更新 frpc 配置并重新加载。
        </template>
      </el-alert>
    </el-form>

    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="saving" @click="handleSave">
        {{ isEdit ? '保存修改' : '添加通道' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { createProxy, updateProxy } from '@/api'
import type { Proxy } from '@/types'

const props = defineProps<{
  modelValue: boolean
  proxy: Proxy | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: []
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v)
})

const isEdit = computed(() => !!props.proxy)
const formRef = ref<FormInstance>()
const saving = ref(false)

const form = reactive({
  name: '',
  type: 'tcp',
  localIP: '',
  localPort: 3389,
  remotePort: 6000,
  description: ''
})

const rules: FormRules = {
  name: [
    { required: true, message: '请输入通道名称', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_\-]+$/, message: '只能包含字母、数字、下划线和连字符', trigger: 'blur' }
  ],
  type: [{ required: true, message: '请选择协议类型', trigger: 'change' }],
  localIP: [
    { required: true, message: '请输入本地 IP', trigger: 'blur' },
    {
      pattern: /^(\d{1,3}\.){3}\d{1,3}$/,
      message: '请输入有效的 IP 地址',
      trigger: 'blur'
    }
  ],
  localPort: [{ required: true, type: 'number', message: '请输入本地端口', trigger: 'blur' }],
  remotePort: [{ required: true, type: 'number', message: '请输入远程端口', trigger: 'blur' }]
}

watch(() => props.proxy, (p) => {
  if (p) {
    form.name = p.name
    form.type = p.type
    form.localIP = p.localIP
    form.localPort = p.localPort
    form.remotePort = p.remotePort
    form.description = p.description
  }
}, { immediate: true })

function resetForm() {
  form.name = ''
  form.type = 'tcp'
  form.localIP = ''
  form.localPort = 3389
  form.remotePort = 6000
  form.description = ''
  formRef.value?.clearValidate()
}

async function handleSave() {
  if (!await formRef.value?.validate().catch(() => false)) return
  saving.value = true
  try {
    if (isEdit.value && props.proxy) {
      await updateProxy(props.proxy.id, form)
      ElMessage.success('通道已更新')
    } else {
      await createProxy(form)
      ElMessage.success('通道已添加')
    }
    emit('saved')
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { message?: string } } })
      ?.response?.data?.message ?? '操作失败'
    ElMessage.error(msg)
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.field-hint {
  font-size: 12px;
  color: #aaa;
  margin-top: 4px;
  line-height: 1.4;
}
</style>
