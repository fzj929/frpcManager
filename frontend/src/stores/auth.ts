import { defineStore } from 'pinia'
import { authLogin } from '@/api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') ?? '',
    username: localStorage.getItem('username') ?? ''
  }),
  getters: {
    isLoggedIn: (state) => !!state.token
  },
  actions: {
    async login(username: string, password: string) {
      const res = await authLogin(username, password)
      this.token = res.data.token
      this.username = res.data.username
      localStorage.setItem('token', res.data.token)
      localStorage.setItem('username', res.data.username)
    },
    logout() {
      this.token = ''
      this.username = ''
      localStorage.removeItem('token')
      localStorage.removeItem('username')
    }
  }
})
