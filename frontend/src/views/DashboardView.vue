<template>
  <div class="dashboard">
    <div class="page-title">
      <h2>仪表板</h2>
      <el-button :loading="refreshing" @click="loadData" :icon="Refresh">刷新</el-button>
    </div>

    <!-- Stats Cards -->
    <el-row :gutter="20" class="stats-row">
      <el-col :xs="12" :sm="6">
        <div class="stat-card">
          <div class="stat-icon" style="background: linear-gradient(135deg, #409EFF, #66b1ff)">
            <el-icon size="28"><Connection /></el-icon>
          </div>
          <div class="stat-body">
            <div class="stat-value">{{ stats.total }}</div>
            <div class="stat-label">总通道数</div>
          </div>
        </div>
      </el-col>
      <el-col :xs="12" :sm="6">
        <div class="stat-card">
          <div class="stat-icon" style="background: linear-gradient(135deg, #67C23A, #95d475)">
            <el-icon size="28"><CircleCheck /></el-icon>
          </div>
          <div class="stat-body">
            <div class="stat-value">{{ stats.running }}</div>
            <div class="stat-label">运行中</div>
          </div>
        </div>
      </el-col>
      <el-col :xs="12" :sm="6">
        <div class="stat-card">
          <div class="stat-icon" style="background: linear-gradient(135deg, #E6A23C, #f3d19e)">
            <el-icon size="28"><Monitor /></el-icon>
          </div>
          <div class="stat-body">
            <div class="stat-value">{{ stats.tcp }}</div>
            <div class="stat-label">TCP 通道</div>
          </div>
        </div>
      </el-col>
      <el-col :xs="12" :sm="6">
        <div class="stat-card">
          <div class="stat-icon" style="background: linear-gradient(135deg, #909399, #c0c4cc)">
            <el-icon size="28"><Cpu /></el-icon>
          </div>
          <div class="stat-body">
            <div class="stat-value">{{ stats.udp }}</div>
            <div class="stat-label">UDP 通道</div>
          </div>
        </div>
      </el-col>
    </el-row>

    <!-- Server Info + Active Proxies -->
    <el-row :gutter="20" style="margin-top: 20px">
      <el-col :xs="24" :sm="12">
        <el-card class="info-card">
          <template #header>
            <div class="card-header">
              <el-icon><Cpu /></el-icon>
              <span>服务器信息</span>
            </div>
          </template>
          <el-descriptions :column="1" border v-if="config">
            <el-descriptions-item label="服务器地址">
              {{ config.serverAddr }}:{{ config.serverPort }}
            </el-descriptions-item>
            <el-descriptions-item label="认证方式">
              <el-tag size="small">{{ config.authMethod }}</el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="Web 管理地址">
              {{ config.webServerAddr }}:{{ config.webServerPort }}
            </el-descriptions-item>
            <el-descriptions-item label="frpc 状态">
              <el-tag type="success" size="small">
                <el-icon style="margin-right: 4px"><CircleCheck /></el-icon>运行中
              </el-tag>
            </el-descriptions-item>
          </el-descriptions>
          <div v-else class="no-data">
            <el-icon color="#F56C6C"><WarningFilled /></el-icon>
            <span>无法连接到 frpc API</span>
          </div>
        </el-card>
      </el-col>

      <el-col :xs="24" :sm="12">
        <el-card class="info-card">
          <template #header>
            <div class="card-header">
              <el-icon><DataAnalysis /></el-icon>
              <span>活动通道</span>
            </div>
          </template>
          <div v-if="activeProxies.length === 0" class="no-data">
            <el-icon color="#909399"><CircleClose /></el-icon>
            <span>暂无运行中的通道</span>
          </div>
          <div v-else>
            <div v-for="p in activeProxies.slice(0, 6)" :key="p.id" class="active-proxy-item">
              <div class="proxy-name-row">
                <el-tag :type="p.type === 'tcp' ? 'primary' : 'warning'" size="small" round>
                  {{ p.type.toUpperCase() }}
                </el-tag>
                <span class="proxy-name">{{ p.name }}</span>
              </div>
              <div class="proxy-addr">{{ p.remoteAddr }}</div>
            </div>
            <div v-if="activeProxies.length > 6" class="more-hint">
              +{{ activeProxies.length - 6 }} 个通道
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh, CircleCheck, Monitor, Cpu, Connection, DataAnalysis, CircleClose, WarningFilled } from '@element-plus/icons-vue'
import { fetchProxies, fetchConfig } from '@/api'
import type { Proxy, FrpcConfig } from '@/types'

const proxies = ref<Proxy[]>([])
const config = ref<FrpcConfig | null>(null)
const refreshing = ref(false)

const stats = computed(() => ({
  total: proxies.value.length,
  running: proxies.value.filter(p => p.status === 'running').length,
  tcp: proxies.value.filter(p => p.type === 'tcp').length,
  udp: proxies.value.filter(p => p.type === 'udp').length
}))

const activeProxies = computed(() => proxies.value.filter(p => p.status === 'running'))

async function loadData() {
  refreshing.value = true
  try {
    const [proxyRes, configRes] = await Promise.allSettled([fetchProxies(), fetchConfig()])
    if (proxyRes.status === 'fulfilled') proxies.value = proxyRes.value.data
    if (configRes.status === 'fulfilled') config.value = configRes.value.data
  } catch {
    ElMessage.error('加载数据失败')
  } finally {
    refreshing.value = false
  }
}

onMounted(loadData)
</script>

<style scoped>
.dashboard { }

.page-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.page-title h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  color: #1a1a2e;
}

.stats-row .el-col { margin-bottom: 16px; }

.stat-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 2px 12px rgba(0,0,0,0.05);
  transition: transform 0.2s;
}

.stat-card:hover { transform: translateY(-2px); }

.stat-icon {
  width: 56px;
  height: 56px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  flex-shrink: 0;
}

.stat-value {
  font-size: 32px;
  font-weight: 700;
  color: #1a1a2e;
  line-height: 1;
}

.stat-label {
  font-size: 13px;
  color: #888;
  margin-top: 4px;
}

.info-card {
  height: 100%;
  border-radius: 12px;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

.no-data {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 24px;
  color: #999;
  font-size: 14px;
}

.active-proxy-item {
  padding: 10px 0;
  border-bottom: 1px solid #f5f5f5;
}

.active-proxy-item:last-child { border-bottom: none; }

.proxy-name-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.proxy-name {
  font-weight: 500;
  font-size: 14px;
}

.proxy-addr {
  font-size: 12px;
  color: #888;
  padding-left: 4px;
}

.more-hint {
  text-align: center;
  font-size: 12px;
  color: #aaa;
  padding-top: 8px;
}
</style>
