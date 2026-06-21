import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/LoginView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/setup',
      name: 'Setup',
      component: () => import('@/views/SetupView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/',
      component: () => import('@/components/AppLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          redirect: '/dashboard'
        },
        {
          path: 'dashboard',
          name: 'Dashboard',
          component: () => import('@/views/DashboardView.vue')
        },
        {
          path: 'proxies',
          name: 'Proxies',
          component: () => import('@/views/ProxiesView.vue')
        },
        {
          path: 'wake',
          name: 'WakeOnLan',
          component: () => import('@/views/WakeOnLanView.vue')
        },
        {
          path: 'wake-records',
          name: 'WakeRecords',
          component: () => import('@/views/WakeRecordsView.vue')
        },
        {
          path: 'https-proxies',
          name: 'HttpsProxies',
          component: () => import('@/views/HttpsProxiesView.vue')
        },
        {
          path: 'audit-logs',
          name: 'AuditLogs',
          component: () => import('@/views/AuditLogsView.vue')
        },
        {
          path: 'settings',
          name: 'Settings',
          component: () => import('@/views/SettingsView.vue')
        }
      ]
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/'
    }
  ]
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (to.path !== '/setup') {
    try {
      const { fetchSetupStatus } = await import('@/api')
      const res = await fetchSetupStatus()
      if (res.data.required) return '/setup'
    } catch {
      // If the API is unavailable, continue with the normal auth guard.
    }
  }
  if (to.meta.requiresAuth !== false && !auth.isLoggedIn) {
    return '/login'
  }
  if (to.path === '/setup') {
    try {
      const { fetchSetupStatus } = await import('@/api')
      const res = await fetchSetupStatus()
      if (!res.data.required) return auth.isLoggedIn ? '/dashboard' : '/login'
    } catch {
      return true
    }
  }
  if (to.path === '/login' && auth.isLoggedIn) {
    return '/dashboard'
  }
})

export default router
