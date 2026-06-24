import { defineStore } from 'pinia'
import { authLogin } from '@/api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') ?? '',
    username: localStorage.getItem('username') ?? '',
    role: localStorage.getItem('role') ?? ''
  }),
  getters: {
    isLoggedIn: (state) => !!state.token,
    isAdmin: (state) => state.role === 'admin'
  },
  actions: {
    async login(username: string, password: string) {
      const res = await authLogin(username, password)
      this.token = res.data.token
      this.username = res.data.username
      this.role = res.data.role
      localStorage.setItem('token', res.data.token)
      localStorage.setItem('username', res.data.username)
      localStorage.setItem('role', res.data.role)
    },
    logout() {
      this.token = ''
      this.username = ''
      this.role = ''
      localStorage.removeItem('token')
      localStorage.removeItem('username')
      localStorage.removeItem('role')
    }
  }
})
