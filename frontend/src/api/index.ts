import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 15000
})

api.interceptors.request.use(config => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  res => res,
  err => {
    if (err.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('username')
      window.location.href = '/login'
    }
    return Promise.reject(err)
  }
)

// Auth
export const authLogin = (username: string, password: string) =>
  api.post('/auth/login', { username, password })

export const authChangePassword = (currentPassword: string, newPassword: string) =>
  api.post('/auth/change-password', { currentPassword, newPassword })

export const authMe = () => api.get('/auth/me')

// Proxies
export const fetchProxies = () => api.get('/proxies')
export const createProxy = (data: object) => api.post('/proxies', data)
export const updateProxy = (id: number, data: object) => api.put(`/proxies/${id}`, data)
export const deleteProxy = (id: number) => api.delete(`/proxies/${id}`)
export const enableProxy = (id: number, durationMinutes: number | null = null) =>
  api.put(`/proxies/${id}/enable`, { durationMinutes })
export const disableProxy = (id: number) => api.put(`/proxies/${id}/disable`)
export const syncFromFrpc = () => api.post('/proxies/sync')

// Config & Status
export const fetchConfig = () => api.get('/config')
export const saveConfig = (data: object) => api.put('/config', data)
export const fetchStatus = () => api.get('/config/status')
export const reloadFrpc = () => api.post('/config/reload')

// Wake-on-LAN
export const wakeOnLan = (data: object) => api.post('/wake-on-lan', data)
