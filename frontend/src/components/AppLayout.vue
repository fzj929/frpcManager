<template>
  <el-container class="layout-wrapper">
    <el-aside :width="isCollapsed ? '64px' : '220px'" class="sidebar">
      <div class="sidebar-logo" @click="router.push('/dashboard')">
        <el-icon size="24" color="#409EFF"><Connection /></el-icon>
        <span v-if="!isCollapsed" class="logo-text">FrpC 管理平台</span>
      </div>

      <el-menu
        :default-active="activeMenu"
        :collapse="isCollapsed"
        background-color="#1e1e2e"
        text-color="#a0a0b0"
        active-text-color="#409EFF"
        router
        class="sidebar-menu"
      >
        <el-menu-item index="/dashboard">
          <el-icon><DataBoard /></el-icon>
          <template #title>仪表板</template>
        </el-menu-item>
        <el-menu-item index="/proxies">
          <el-icon><Connection /></el-icon>
          <template #title>通道管理</template>
        </el-menu-item>
        <el-sub-menu index="/wake-menu">
          <template #title>
            <el-icon><Monitor /></el-icon>
            <span>主机唤醒</span>
          </template>
          <el-menu-item index="/wake">
            <el-icon><SwitchButton /></el-icon>
            <template #title>唤醒主机</template>
          </el-menu-item>
          <el-menu-item index="/wake-mac-addresses">
            <el-icon><Monitor /></el-icon>
            <template #title>MAC地址管理</template>
          </el-menu-item>
          <el-menu-item index="/wake-records">
            <el-icon><Timer /></el-icon>
            <template #title>唤醒记录</template>
          </el-menu-item>
        </el-sub-menu>
        <el-menu-item index="/https-proxies">
          <el-icon><Lock /></el-icon>
          <template #title>HTTPS代理</template>
        </el-menu-item>
        <el-menu-item v-if="auth.isAdmin" index="/audit-logs">
          <el-icon><Document /></el-icon>
          <template #title>操作日志</template>
        </el-menu-item>
        <el-menu-item v-if="auth.isAdmin" index="/users">
          <el-icon><User /></el-icon>
          <template #title>用户管理</template>
        </el-menu-item>
        <el-menu-item index="/settings">
          <el-icon><Setting /></el-icon>
          <template #title>系统设置</template>
        </el-menu-item>
      </el-menu>

      <div class="sidebar-footer">
        <el-tooltip :content="isCollapsed ? '展开' : '收起'" placement="right">
          <el-button text @click="isCollapsed = !isCollapsed" class="collapse-btn">
            <el-icon><Fold v-if="!isCollapsed" /><Expand v-else /></el-icon>
          </el-button>
        </el-tooltip>
      </div>
    </el-aside>

    <el-container>
      <el-header class="header">
        <div class="header-left">
          <el-breadcrumb separator="/">
            <el-breadcrumb-item :to="{ path: '/dashboard' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item>{{ currentPageTitle }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        <div class="header-right">
          <el-dropdown trigger="click" @command="handleCommand">
            <div class="user-info">
              <el-avatar :size="32" style="background-color: #409EFF">
                {{ auth.username.charAt(0).toUpperCase() }}
              </el-avatar>
              <span class="username">{{ auth.username }}</span>
              <el-icon><ArrowDown /></el-icon>
            </div>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="settings">
                  <el-icon><Setting /></el-icon> 系统设置
                </el-dropdown-item>
                <el-dropdown-item command="logout" divided>
                  <el-icon><SwitchButton /></el-icon> 退出登录
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <el-main class="main-content">
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </el-main>
    </el-container>

    <nav class="mobile-tabbar" aria-label="移动端主导航">
      <button
        type="button"
        :class="['mobile-tabbar-item', route.path === '/dashboard' ? 'active' : '']"
        @click="navigateTo('/dashboard')"
      >
        <el-icon><DataBoard /></el-icon>
        <span>仪表盘</span>
      </button>
      <button
        type="button"
        :class="['mobile-tabbar-item', route.path === '/proxies' ? 'active' : '']"
        @click="navigateTo('/proxies')"
      >
        <el-icon><Connection /></el-icon>
        <span>通道</span>
      </button>
      <button
        type="button"
        :class="['mobile-tabbar-item', isWakeActive ? 'active' : '']"
        @click="mobileWakeSheetVisible = true"
      >
        <el-icon><Monitor /></el-icon>
        <span>唤醒</span>
      </button>
      <button
        type="button"
        :class="['mobile-tabbar-item', route.path === '/https-proxies' ? 'active' : '']"
        @click="navigateTo('/https-proxies')"
      >
        <el-icon><Lock /></el-icon>
        <span>HTTPS</span>
      </button>
      <button
        type="button"
        :class="['mobile-tabbar-item', route.path === '/settings' ? 'active' : '']"
        @click="navigateTo('/settings')"
      >
        <el-icon><Setting /></el-icon>
        <span>设置</span>
      </button>
    </nav>

    <el-drawer
      v-model="mobileWakeSheetVisible"
      direction="btt"
      size="auto"
      :with-header="false"
      class="mobile-action-drawer"
    >
      <div class="mobile-sheet">
        <div class="mobile-sheet-handle"></div>
        <div class="mobile-sheet-title">主机唤醒</div>
        <div class="mobile-sheet-grid">
          <button
            type="button"
            :class="['mobile-sheet-action', route.path === '/wake' ? 'active' : '']"
            @click="navigateTo('/wake')"
          >
            <el-icon><SwitchButton /></el-icon>
            <span>唤醒主机</span>
          </button>
          <button
            type="button"
            :class="['mobile-sheet-action', route.path === '/wake-mac-addresses' ? 'active' : '']"
            @click="navigateTo('/wake-mac-addresses')"
          >
            <el-icon><Monitor /></el-icon>
            <span>MAC 地址</span>
          </button>
          <button
            type="button"
            :class="['mobile-sheet-action', route.path === '/wake-records' ? 'active' : '']"
            @click="navigateTo('/wake-records')"
          >
            <el-icon><Timer /></el-icon>
            <span>唤醒记录</span>
          </button>
        </div>
      </div>
    </el-drawer>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { ElMessageBox } from 'element-plus'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const isCollapsed = ref(false)
const mobileWakeSheetVisible = ref(false)

const activeMenu = computed(() => route.path)
const isWakeActive = computed(() => route.path.startsWith('/wake'))

const pageTitles: Record<string, string> = {
  '/dashboard': '仪表板',
  '/proxies': '通道管理',
  '/wake': '唤醒主机',
  '/wake-mac-addresses': 'MAC地址管理',
  '/wake-records': '唤醒记录',
  '/https-proxies': 'HTTPS代理',
  '/audit-logs': '操作日志',
  '/users': '用户管理',
  '/settings': '系统设置'
}

const currentPageTitle = computed(() => pageTitles[route.path] ?? '')

function navigateTo(path: string) {
  mobileWakeSheetVisible.value = false
  if (route.path !== path) {
    router.push(path)
  }
}

async function handleCommand(cmd: string) {
  if (cmd === 'logout') {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })
    auth.logout()
    router.push('/login')
  } else if (cmd === 'settings') {
    router.push('/settings')
  }
}
</script>

<style scoped>
.layout-wrapper {
  height: 100vh;
  overflow: hidden;
}

.sidebar {
  background-color: #1e1e2e;
  display: flex;
  flex-direction: column;
  transition: width 0.3s;
  overflow: hidden;
}

.sidebar-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 16px;
  cursor: pointer;
  border-bottom: 1px solid #2a2a3e;
  min-height: 64px;
}

.logo-text {
  color: #fff;
  font-size: 16px;
  font-weight: 600;
  white-space: nowrap;
}

.sidebar-menu {
  flex: 1;
  border: none;
}

.sidebar-footer {
  padding: 12px;
  border-top: 1px solid #2a2a3e;
  display: flex;
  justify-content: center;
}

.collapse-btn {
  color: #a0a0b0;
}

.header {
  background: #fff;
  border-bottom: 1px solid #e8e8e8;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  height: 56px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 8px;
  transition: background 0.2s;
}

.user-info:hover {
  background: #f5f5f5;
}

.username {
  font-size: 14px;
  color: #333;
}

.main-content {
  background: #f5f7fa;
  overflow-y: auto;
  padding: 24px;
}

.mobile-tabbar {
  display: none;
}

.mobile-tabbar-item,
.mobile-sheet-action {
  border: 0;
  font: inherit;
  cursor: pointer;
}

@media (max-width: 768px) {
  .layout-wrapper {
    display: block;
    height: 100dvh;
  }

  .sidebar {
    display: none;
  }

  .mobile-tabbar {
    display: flex;
    position: fixed;
    left: 0;
    right: 0;
    bottom: 0;
    z-index: 1000;
    height: calc(64px + env(safe-area-inset-bottom));
    width: 100%;
    padding: 6px 8px calc(6px + env(safe-area-inset-bottom));
    gap: 4px;
    background: #1e1e2e;
    border-top: 1px solid #2a2a3e;
    box-shadow: 0 -8px 24px rgba(0, 0, 0, 0.16);
  }

  .mobile-tabbar-item {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 4px;
    min-height: 52px;
    padding: 6px 2px;
    color: #a0a0b0;
    background: transparent;
    border-radius: 10px;
    transition: background 0.2s, color 0.2s;
  }

  .mobile-tabbar-item .el-icon {
    font-size: 18px;
  }

  .mobile-tabbar-item span {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 11px;
    line-height: 1.1;
  }

  .mobile-tabbar-item.active {
    color: #409EFF;
    background: rgba(64, 158, 255, 0.14);
  }

  .header {
    height: 48px;
    padding: 0 12px;
  }

  .header-left {
    min-width: 0;
  }

  .header :deep(.el-breadcrumb) {
    font-size: 12px;
  }

  .username {
    display: none;
  }

  .user-info {
    padding: 2px 0;
  }

  .main-content {
    height: calc(100dvh - 48px);
    padding: 12px;
    padding-bottom: calc(82px + env(safe-area-inset-bottom));
  }
}

.mobile-sheet {
  padding: 10px 14px calc(18px + env(safe-area-inset-bottom));
}

.mobile-sheet-handle {
  width: 36px;
  height: 4px;
  margin: 0 auto 12px;
  border-radius: 999px;
  background: #dcdfe6;
}

.mobile-sheet-title {
  margin-bottom: 14px;
  font-size: 17px;
  font-weight: 600;
  color: #303133;
  text-align: center;
}

.mobile-sheet-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.mobile-sheet-action {
  display: flex;
  min-height: 82px;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px 6px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  color: #606266;
  background: #fff;
}

.mobile-sheet-action .el-icon {
  font-size: 22px;
  color: #409EFF;
}

.mobile-sheet-action span {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.mobile-sheet-action.active {
  border-color: #409EFF;
  color: #409EFF;
  background: #ecf5ff;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>

<style>
html, body, #app {
  margin: 0;
  padding: 0;
  height: 100%;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
}

:root {
  --el-menu-bg-color: #1e1e2e !important;
}

* {
  box-sizing: border-box;
}

@media (max-width: 768px) {
  .el-card__body {
    padding: 14px !important;
  }

  .el-dialog {
    --el-dialog-width: calc(100vw - 24px) !important;
    margin-top: 5vh !important;
  }

  .el-form-item {
    margin-bottom: 18px;
  }

  .el-form-item__label {
    line-height: 1.3;
  }

  .el-message-box {
    width: calc(100vw - 24px) !important;
  }
}
</style>
