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

export interface WakeLog {
  id: number
  macAddress: string
  broadcastAddress: string
  port: number
  source: string
  username: string
  ipAddress: string
  success: boolean
  message: string
  createdAt: string
}

export interface WakeSchedule {
  id: number
  name: string
  macAddress: string
  broadcastAddress: string
  port: number
  timeOfDay: string
  isEnabled: boolean
  lastRunAt: string | null
  createdAt: string
  updatedAt: string | null
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

export interface HttpsProxyRule {
  id: number
  name: string
  listenPort: number
  targetUrl: string
  certificateMode: string
  hasCustomCertificate: boolean
  hasPrivateKey: boolean
  description: string
  isEnabled: boolean
  createdAt: string
  updatedAt: string | null
}
