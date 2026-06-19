<template>
  <el-dialog
    v-model="visible"
    title="选择通道开放时长"
    width="440px"
    :close-on-click-modal="false"
  >
    <div class="timed-enable-body">
      <div class="proxy-info">
        <el-icon size="18"><Connection /></el-icon>
        <span>正在启用：</span>
        <strong>{{ proxyName }}</strong>
        <el-tag :type="proxyType === 'tcp' ? 'primary' : 'warning'" size="small" round style="margin-left: 8px">
          {{ proxyType?.toUpperCase() }}
        </el-tag>
      </div>

      <el-alert
        type="warning"
        show-icon
        :closable="false"
        style="margin: 16px 0"
      >
        <template #title>安全提示</template>
        建议设置开放时限，避免通道长期暴露，降低被入侵风险。
      </el-alert>

      <div class="duration-label">请选择通道开放时长：</div>
      <div class="duration-grid">
        <div
          v-for="opt in options"
          :key="String(opt.value)"
          :class="['duration-item', selected === opt.value ? 'active' : '']"
          @click="selected = opt.value"
        >
          <div class="duration-icon">
            <el-icon size="22"><component :is="opt.icon" /></el-icon>
          </div>
          <div class="duration-text">{{ opt.label }}</div>
          <div class="duration-sub">{{ opt.sub }}</div>
          <el-icon v-if="selected === opt.value" class="check-icon" size="14"><Check /></el-icon>
        </div>
      </div>

      <div v-if="selected !== null" class="expire-preview">
        <el-icon><Timer /></el-icon>
        将于 {{ previewExpiry }} 自动关闭
      </div>
      <div v-else class="expire-preview unlimited">
        <el-icon><Unlock /></el-icon>
        无时间限制，需手动关闭
      </div>
    </div>

    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="loading" @click="confirm">
        确认启用
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Connection, Timer, Unlock, Check } from '@element-plus/icons-vue'

const props = defineProps<{
  modelValue: boolean
  proxyName: string
  proxyType: string
  loading: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: [durationMinutes: number | null]
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v)
})

const selected = ref<number | null>(60)

const options = [
  { value: 30,   label: '30 分钟', sub: '短时访问',   icon: 'AlarmClock' },
  { value: 60,   label: '1 小时',  sub: '推荐',       icon: 'Timer' },
  { value: 120,  label: '2 小时',  sub: '临时使用',   icon: 'Clock' },
  { value: 480,  label: '8 小时',  sub: '工作日',     icon: 'Sunny' },
  { value: 720,  label: '12 小时', sub: '半天',       icon: 'Sunset' },
  { value: null, label: '不限制',  sub: '手动关闭',   icon: 'Unlock' },
]

const previewExpiry = computed(() => {
  if (selected.value === null) return ''
  const t = new Date(Date.now() + selected.value * 60 * 1000)
  return t.toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
})

function confirm() {
  emit('confirm', selected.value)
}
</script>

<style scoped>
.timed-enable-body { padding: 4px 0; }

.proxy-info {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  padding: 10px 14px;
  background: #f5f7fa;
  border-radius: 8px;
  color: #555;
}

.duration-label {
  font-size: 13px;
  color: #666;
  margin-bottom: 12px;
}

.duration-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
  margin-bottom: 16px;
}

.duration-item {
  position: relative;
  padding: 14px 10px 10px;
  border: 2px solid #e4e7ed;
  border-radius: 10px;
  cursor: pointer;
  text-align: center;
  transition: all 0.2s;
  background: #fff;
}

.duration-item:hover {
  border-color: #409EFF;
  background: #f0f7ff;
}

.duration-item.active {
  border-color: #409EFF;
  background: #e8f4ff;
}

.duration-icon {
  color: #409EFF;
  margin-bottom: 6px;
}

.duration-text {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.duration-sub {
  font-size: 11px;
  color: #aaa;
  margin-top: 2px;
}

.check-icon {
  position: absolute;
  top: 6px;
  right: 6px;
  color: #409EFF;
}

.expire-preview {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #E6A23C;
  padding: 8px 12px;
  background: #fdf6ec;
  border-radius: 6px;
}

.expire-preview.unlimited {
  color: #909399;
  background: #f5f5f5;
}

@media (max-width: 768px) {
  .duration-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .duration-item {
    min-height: 82px;
    padding: 12px 8px 8px;
  }
}
</style>
