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
  createdByUserId: number | null
  createdByUsername: string
  canManage: boolean
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
  role: string
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

export interface WakePingRequest {
  host: string
  timeoutMs: number
}

export interface WakePingResponse {
  host: string
  isOnline: boolean
  roundtripTimeMs: number | null
  status: string
  message: string
}

export interface WakeLog {
  id: number
  macAddress: string
  macName: string
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
  macName: string
  broadcastAddress: string
  port: number
  timeOfDay: string
  scheduleMode: string
  daysOfWeek: string
  specificDate: string | null
  isEnabled: boolean
  lastRunAt: string | null
  createdAt: string
  updatedAt: string | null
}

export interface WakeMacAddress {
  id: number
  macAddress: string
  name: string
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
  createdByUserId: number | null
  createdByUsername: string
  canManage: boolean
  certificateInfo: HttpsProxyCertificateInfo | null
  createdAt: string
  updatedAt: string | null
}

export interface HttpsProxyCertificateInfo {
  subject: string
  issuer: string
  notBefore: string
  notAfter: string
  daysRemaining: number
  isExpired: boolean
  isExpiringSoon: boolean
  matchesHost: boolean
  host: string
  domains: string[]
  error: string | null
}

export interface UserAccount {
  id: number
  username: string
  role: string
  isDisabled: boolean
  createdAt: string
  updatedAt: string | null
}
