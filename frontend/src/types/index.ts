export interface Proxy {
  id: number
  name: string
  type: string
  localIP: string
  localPort: number
  remotePort: number
  description: string
  isEnabled: boolean
  status: string
  remoteAddr: string
  errorMsg: string
  createdAt: string
  updatedAt: string | null
  expiresAt: string | null
}

export interface FrpcConfig {
  serverAddr: string
  serverPort: number
  authMethod: string
  authToken: string
  webServerAddr: string
  webServerPort: number
}

export interface ProxyStatusItem {
  name: string
  type: string
  status: string
  localAddr: string
  remoteAddr: string
  error: string
}

export interface LoginResponse {
  token: string
  username: string
  expiresAt: string
}

export interface WakeOnLanRequest {
  macAddress: string
  broadcastAddress: string
  port: number
}

export interface WakeOnLanResponse {
  macAddress: string
  broadcastAddress: string
  port: number
  message: string
}

export interface AuditLog {
  id: number
  username: string
  action: string
  target: string
  details: string
  ipAddress: string
  success: boolean
  createdAt: string
}

export interface HealthStatus {
  status: string
  database: string
  frpc: string
  checkedAt: string
}
