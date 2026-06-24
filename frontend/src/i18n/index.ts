import { computed, nextTick, ref } from 'vue'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import en from 'element-plus/es/locale/lang/en'

export type Language = 'zh' | 'en'

const storageKey = 'language'
const savedLanguage = localStorage.getItem(storageKey)
export const language = ref<Language>(savedLanguage === 'en' ? 'en' : 'zh')

export const elementLocale = computed(() => language.value === 'en' ? en : zhCn)

export function setLanguage(value: Language) {
  language.value = value
  localStorage.setItem(storageKey, value)
}

const enText: Record<string, string> = {
  'FrpC 管理平台': 'FrpC Manager',
  '请登录以管理您的 frpc 通道': 'Sign in to manage your frpc tunnels',
  '用户名': 'Username',
  '密码': 'Password',
  '登 录': 'Sign In',
  '首次启动请先完成管理员初始化': 'Complete administrator setup on first launch',
  '首次启动初始化': 'First-Run Setup',
  '请创建管理员账号。初始化完成后将进入登录页面。': 'Create an administrator account. After setup, you will be redirected to the login page.',
  '确认密码': 'Confirm Password',
  '至少 8 位': 'At least 8 characters',
  '完成初始化': 'Complete Setup',

  '仪表板': 'Dashboard',
  '刷新': 'Refresh',
  '总通道数': 'Total Tunnels',
  '运行中': 'Running',
  'TCP 通道': 'TCP Tunnels',
  'UDP 通道': 'UDP Tunnels',
  '服务器信息': 'Server Information',
  '服务器地址': 'Server Address',
  '认证方式': 'Auth Method',
  'Web 管理地址': 'Web Admin Address',
  'frpc 状态': 'frpc Status',
  '无法连接到 frpc API': 'Unable to connect to frpc API',
  '活动通道': 'Active Tunnels',
  '暂无运行中的通道': 'No running tunnels',
  '个通道': 'tunnels',

  '首页': 'Home',
  '通道管理': 'Tunnels',
  '主机唤醒': 'Wake-on-LAN',
  '唤醒主机': 'Wake Host',
  'MAC地址管理': 'MAC Addresses',
  '唤醒记录': 'Wake Records',
  'HTTPS代理': 'HTTPS Proxy',
  'HTTPS 代理': 'HTTPS Proxy',
  '操作日志': 'Audit Logs',
  '用户管理': 'Users',
  '系统设置': 'Settings',
  '展开': 'Expand',
  '收起': 'Collapse',
  '退出登录': 'Sign Out',
  '确定': 'OK',
  '取消': 'Cancel',
  '提示': 'Prompt',

  '搜索通道名称...': 'Search tunnel name...',
  '协议类型': 'Protocol',
  '全部类型': 'All Types',
  '运行状态': 'Status',
  '全部状态': 'All Status',
  '已停用': 'Disabled',
  '从 frpc 同步': 'Sync from frpc',
  '添加通道': 'Add Tunnel',
  '状态': 'Status',
  '通道名称': 'Tunnel Name',
  '类型': 'Type',
  '本地地址': 'Local Address',
  '远程端口': 'Remote Port',
  '创建者': 'Owner',
  '未分配': 'Unassigned',
  '禁用': 'Disabled',
  '历史配置': 'Legacy',
  '远程地址': 'Remote Address',
  '启用': 'Enable',
  '操作': 'Actions',
  '无时限': 'No time limit',
  '即将关闭': 'Closing soon',
  '错误': 'Error',
  '未知': 'Unknown',
  '删除确认': 'Delete Confirmation',
  '删除': 'Delete',
  '编辑': 'Edit',
  '保存': 'Save',
  '备注': 'Notes',
  '描述': 'Description',
  '编辑通道': 'Edit Tunnel',
  '保存修改': 'Save Changes',
  '通道已更新': 'Tunnel updated',
  '通道已添加': 'Tunnel added',
  '操作失败': 'Operation failed',
  '选择通道开放时长': 'Select Tunnel Duration',
  '正在启用：': 'Enabling:',
  '安全提示': 'Security Tip',
  '建议设置开放时限，避免通道长期暴露，降低被入侵风险。': 'Set an exposure time limit to reduce long-running public access risk.',
  '请选择通道开放时长：': 'Select tunnel duration:',
  '无时间限制，需手动关闭': 'No time limit; close manually',
  '确认启用': 'Confirm Enable',
  '30 分钟': '30 minutes',
  '1 小时': '1 hour',
  '2 小时': '2 hours',
  '8 小时': '8 hours',
  '12 小时': '12 hours',
  '不限制': 'Unlimited',
  '短时访问': 'Short access',
  '推荐': 'Recommended',
  '临时使用': 'Temporary',
  '工作日': 'Workday',
  '半天': 'Half day',
  '手动关闭': 'Manual close',

  '新增代理': 'New Proxy',
  '轻量 HTTPS 反向代理会在本机监听 HTTPS 端口，并转发到内网 HTTP 服务。证书可使用网站默认证书，也可以上传 IIS 证书（.pfx/.p12）或 Nginx 证书（.pem/.crt/.cer + .key）。': 'Lightweight HTTPS reverse proxy listens on a local HTTPS port and forwards to an internal HTTP service. Use the default site certificate, or upload an IIS certificate (.pfx/.p12) or Nginx certificate (.pem/.crt/.cer + .key).',
  '名称': 'Name',
  '访问地址': 'Access URL',
  '目标 HTTP 地址': 'Target HTTP URL',
  '证书': 'Certificate',
  '编辑 HTTPS 代理': 'Edit HTTPS Proxy',
  '新增 HTTPS 代理': 'New HTTPS Proxy',
  '例如：内网管理后台': 'Example: internal admin panel',
  '监听端口': 'Listen Port',
  '用户访问此端口的 HTTPS 地址，例如 8443。': 'HTTPS port users access, for example 8443.',
  '目标地址': 'Target URL',
  '第一版仅支持转发到 HTTP 地址。': 'Only forwarding to HTTP URLs is supported.',
  'frp 通道': 'frp Tunnel',
  '同时创建 frp 通道': 'Create frp tunnel as well',
  '通道会默认停用。创建后请到通道管理中打开通道，外网才能通过 frp 访问该 HTTPS 代理。': 'The tunnel is created disabled. Enable it in tunnel management before external access through frp works.',
  '规则同通道管理：只能包含字母、数字、下划线和连字符。本地 IP 为 127.0.0.1，本地端口和远程端口都使用 HTTPS 监听端口。': 'Same naming rules as tunnels: letters, numbers, underscore, and hyphen only. Local IP is 127.0.0.1; local and remote ports use the HTTPS listen port.',
  '证书来源': 'Certificate Source',
  '默认证书': 'Default Certificate',
  'IIS证书': 'IIS Certificate',
  'Nginx证书': 'Nginx Certificate',
  '选择 .pfx/.p12': 'Choose .pfx/.p12',
  '适用于 Windows IIS 导出的 PFX/P12 证书，通常包含证书和私钥。': 'For PFX/P12 certificates exported from Windows IIS, usually containing certificate and private key.',
  '证书密码': 'Certificate Password',
  'PFX 证书密码': 'PFX certificate password',
  '选择证书文件': 'Choose certificate file',
  "适用于 Nginx/Caddy/Let's Encrypt 的 fullchain.pem、cert.pem、.crt 或 .cer。": "For Nginx/Caddy/Let's Encrypt fullchain.pem, cert.pem, .crt, or .cer.",
  '私钥文件': 'Private Key File',
  '选择 .key 私钥': 'Choose .key private key',
  '私钥密码': 'Private Key Password',
  '没有密码可留空': 'Leave empty if no password',

  '时间': 'Time',
  '用户': 'User',
  '对象': 'Target',
  '结果': 'Result',
  '详情': 'Details',
  '成功': 'Success',
  '失败': 'Failed',

  '账号安全': 'Account Security',
  '当前密码': 'Current Password',
  '新密码': 'New Password',
  '确认新密码': 'Confirm New Password',
  '修改密码': 'Change Password',
  '关于': 'About',
  '版本': 'Version',
  '项目地址': 'Project URL',
  '后端': 'Backend',
  '前端': 'Frontend',
  '数据库': 'Database',
  'frpc 服务器配置': 'frpc Server Config',
  '服务器端口': 'Server Port',
  '认证 Token': 'Auth Token',
  '与服务端配置保持一致': 'Keep consistent with server config',
  'Web 管理界面': 'Web Admin UI',
  '监听地址': 'Listen Address',
  '保存并重新加载': 'Save and Reload',
  '仅重新加载': 'Reload Only',
  '健康检查': 'Health Check',
  '服务状态': 'Service Status',
  '刷新健康状态': 'Refresh Health',
  '配置备份与恢复': 'Backup and Restore',
  '导出配置备份': 'Export Backup',
  '导入并恢复': 'Import and Restore',

  '唤醒历史': 'Wake History',
  '定时唤醒': 'Scheduled Wake',
  '新增定时唤醒': 'New Schedule',
  '任务名称': 'Task Name',
  '主机名称': 'Host Name',
  'MAC 地址': 'MAC Address',
  '广播地址': 'Broadcast Address',
  '端口': 'Port',
  '执行规则': 'Schedule Rule',
  '上次执行': 'Last Run',
  '唤醒': 'Wake',
  '再次唤醒': 'Wake Again',
  '来源': 'Source',
  '消息': 'Message',
  '来源 IP': 'Source IP',
  '编辑定时唤醒': 'Edit Schedule',
  '每天时间': 'Daily Time',
  '执行方式': 'Schedule Mode',
  '每天': 'Daily',
  '每周': 'Weekly',
  '指定日期': 'Specific Date',
  '选择星期': 'Weekdays',
  '选择日期': 'Select Date',
  '周一': 'Mon',
  '周二': 'Tue',
  '周三': 'Wed',
  '周四': 'Thu',
  '周五': 'Fri',
  '周六': 'Sat',
  '周日': 'Sun',
  '定时': 'Scheduled',
  '手动': 'Manual',

  '新增用户': 'New User',
  '角色': 'Role',
  '管理员': 'Administrator',
  '普通用户': 'Normal User',
  '正常': 'Active',
  '已禁用': 'Disabled',
  '创建时间': 'Created At',
  '编辑用户': 'Edit User',
  '重置密码': 'Reset Password',
  '用户已更新': 'User updated',
  '用户已创建': 'User created',
  '密码已重置': 'Password reset',
  '重置': 'Reset',

  '输入用户名或名称搜索': 'Search username or name',
  '添加 MAC 地址': 'Add MAC Address',
  '更新 MAC 地址': 'Update MAC Address',
  '发送魔术包': 'Send Magic Packet',
  '使用说明': 'Instructions',
  '支持格式': 'Supported Formats',
  '新增 MAC 地址': 'Add MAC Address',
  '编辑 MAC 地址': 'Edit MAC Address',
  'MAC 地址管理': 'MAC Address Management',
  '发送 Wake-on-LAN 魔术数据包': 'Send Wake-on-LAN Magic Packet',
  '魔术数据包已发送': 'Magic packet sent',
  '发送失败': 'Send failed',
  '定时任务已创建': 'Schedule created',
  '定时任务已更新': 'Schedule updated',
  '定时任务已删除': 'Schedule deleted',
  'MAC 地址已添加': 'MAC address added',
  'MAC 地址已更新': 'MAC address updated',
  'MAC 地址已删除': 'MAC address deleted',
  'MAC 地址格式不正确': 'Invalid MAC address format',
  '未命名': 'Unnamed',
  '默认广播': 'Default broadcast',
  '默认端口': 'Default port',
  '选择时间': 'Select time',
  '请选择执行方式': 'Select execution mode',
  '请选择每周执行的日期': 'Select weekdays',
  '请选择指定日期': 'Select date',
  '请输入任务名称': 'Enter task name',
  '请输入 MAC 地址': 'Enter MAC address',
  '请输入广播地址': 'Enter broadcast address',
  '请输入端口': 'Enter port',
  '例如：办公室电脑': 'Example: Office PC',
  '例如：张三的主机；留空则默认使用 MAC 地址': 'Example: Zhang San PC; leave empty to use the MAC address',
  '例如：00:11:22:33:44:55': 'Example: 00:11:22:33:44:55',
  '例如：192.168.0.100': 'Example: 192.168.0.100',
  '例如：3389': 'Example: 3389',
  '例如：6001': 'Example: 6001',
  '例如：rdp-office': 'Example: rdp-office',
  '例如：https-office': 'Example: https-office',
  '例如：soft.mybips.com': 'Example: soft.mybips.com',
  '可以选择已保存的 MAC 地址，也可以直接输入新的 MAC 地址。': 'Select a saved MAC address or enter a new one.',
  '跨网段唤醒时通常需要填写目标网段广播地址，例如 192.168.1.255。': 'For cross-subnet wake, usually enter the target subnet broadcast address, such as 192.168.1.255.',
  'Wake-on-LAN 常用端口为 9，也有设备使用 7。': 'Wake-on-LAN usually uses port 9; some devices use port 7.',
  '目标计算机需要在 BIOS/网卡/系统中开启 Wake-on-LAN，并且网线或电源保持可唤醒状态。': 'The target computer must enable Wake-on-LAN in BIOS, network adapter, or OS settings and remain wake-capable.',
  '支持 00:11:22:33:44:55、00-11-22-33-44-55、001122334455、0011.2233.4455。': 'Supports 00:11:22:33:44:55, 00-11-22-33-44-55, 001122334455, and 0011.2233.4455.',
  '本地 IP': 'Local IP',
  '本地端口': 'Local Port',
  '请输入本地 IP': 'Enter local IP',
  '请输入本地端口': 'Enter local port',
  '请输入远程端口': 'Enter remote port',
  '请输入服务器地址': 'Enter server address',
  '请输入服务器端口': 'Enter server port',
  '请输入监听端口': 'Enter listen port',
  '请输入目标地址': 'Enter target URL',
  '请输入有效的 IP 地址': 'Enter a valid IP address',
  '请选择协议类型': 'Select protocol type',
  '请选择角色': 'Select role',
  '请输入用户名': 'Enter username',
  '请输入密码': 'Enter password',
  '请确认密码': 'Confirm password',
  '请输入当前密码': 'Enter current password',
  '请输入新密码': 'Enter new password',
  '请确认新密码': 'Confirm new password',
  '密码不能少于 6 个字符': 'Password must be at least 6 characters',
  '密码不能少于 8 位': 'Password must be at least 8 characters',
  '两次输入的密码不一致': 'The two passwords do not match',
  '登录成功': 'Login succeeded',
  '登录失败，请检查用户名和密码': 'Login failed. Check username and password.',
  '初始化失败': 'Initialization failed',
  '初始化完成，请登录': 'Initialization complete. Please log in.',
  '保存失败': 'Save failed',
  '删除成功': 'Deleted',
  '删除失败': 'Delete failed',
  '启用失败': 'Enable failed',
  '停用失败': 'Disable failed',
  '停用': 'Disable',
  '代理已启用': 'Proxy enabled',
  '代理已停用': 'Proxy disabled',
  'HTTPS 代理已创建': 'HTTPS proxy created',
  'HTTPS 代理已更新': 'HTTPS proxy updated',
  'HTTPS 代理已删除': 'HTTPS proxy deleted',
  'HTTPS 代理归属已更新': 'HTTPS proxy owner updated',
  '更新 HTTPS 代理归属失败': 'Failed to update HTTPS proxy owner',
  'HTTPS 代理已创建，frp 通道已添加，请到通道管理中打开通道后再访问': 'HTTPS proxy created and frp tunnel added. Enable the tunnel in tunnel management before access.',
  '通道归属已更新': 'Tunnel owner updated',
  '更新通道归属失败': 'Failed to update tunnel owner',
  '通道': 'Tunnel',
  '确定要删除通道': 'Delete tunnel',
  '确定要删除这个定时唤醒任务吗？': 'Delete this wake schedule?',
  '确定要退出登录吗？': 'Log out?',
  '同步失败，请检查 frpc 是否运行': 'Sync failed. Check whether frpc is running.',
  '无法从 frpc 获取配置，frpc 可能未运行': 'Could not get config from frpc. frpc may not be running.',
  '已从 frpc 同步通道配置': 'Tunnel config synced from frpc',
  '已启用（无时间限制）': 'Enabled (no time limit)',
  '配置已保存并重新加载': 'Config saved and reloaded',
  '配置备份已导出': 'Config backup exported',
  '配置已合并导入': 'Config merged and imported',
  '合并导入': 'Merge Import',
  '恢复确认': 'Restore Confirmation',
  '恢复配置失败': 'Restore config failed',
  '导出配置备份失败': 'Export config backup failed',
  '重新加载失败': 'Reload failed',
  '重置失败': 'Reset failed',
  'frpc 已重新加载': 'frpc reloaded',
  '健康检查失败': 'Health check failed',
  '加载数据失败': 'Failed to load data',
  '加载用户失败': 'Failed to load user',
  '加载用户列表失败': 'Failed to load user list',
  '加载通道列表失败': 'Failed to load tunnel list',
  '加载操作日志失败': 'Failed to load audit logs',
  '密码修改成功': 'Password changed',
  '密码修改失败': 'Failed to change password',
  '目标地址必须是 HTTP URL': 'Target URL must be an HTTP URL',
  '无认证': 'No Authentication',
  'Token 认证': 'Token Authentication',
  'frp 服务端的域名或 IP 地址': 'Domain or IP address of the frp server',
  'frp 服务端监听的端口，默认 7000': 'frp server listen port, default 7000',
  'frp 服务端设置的 token，需与服务端一致': 'Token configured on the frp server; must match the server',
  'frp 服务器上开放的公网端口': 'Public port opened on the frp server',
  'frpc 内置 Web 服务的监听地址': 'Listen address of the built-in frpc web service',
  'frpc 内置 Web 服务的监听端口，默认 7400': 'Listen port of the built-in frpc web service, default 7400',
  '需要暴露的内网机器 IP 地址（frpc 所在机器可访问的地址）': 'LAN machine IP to expose, reachable from the frpc host',
  '内网机器上的服务端口（如 RDP=3389）': 'Service port on the LAN machine, such as RDP=3389',
  '唯一标识符，用于区分不同通道，建议使用英文和数字': 'Unique identifier for this tunnel. English letters and numbers are recommended.',
  '只能包含字母、数字、下划线和连字符': 'Only letters, numbers, underscores, and hyphens are allowed',
  '可选，用于备注该通道的用途': 'Optional notes for this tunnel'
}

const textOriginals = new WeakMap<Text, string>()
const attrOriginals = new WeakMap<Element, Map<string, string>>()
let observer: MutationObserver | null = null
let applying = false

export function translateValue(value: string) {
  if (language.value === 'zh') return value
  return enText[value.trim()] ?? value
}

function replaceKeepingWhitespace(value: string, original: string) {
  const leading = original.match(/^\s*/)?.[0] ?? ''
  const trailing = original.match(/\s*$/)?.[0] ?? ''
  return `${leading}${value}${trailing}`
}

function shouldSkip(node: Node) {
  const element = node.nodeType === Node.ELEMENT_NODE
    ? node as Element
    : node.parentElement

  return !!element?.closest('[data-no-translate], script, style, code, pre')
}

function translateTextNode(node: Text) {
  if (shouldSkip(node)) return

  const original = textOriginals.get(node) ?? node.data
  if (!textOriginals.has(node)) textOriginals.set(node, original)

  const trimmed = original.trim()
  if (!trimmed) return

  const translated = language.value === 'en'
    ? enText[trimmed]
    : undefined

  const nextValue = translated
    ? replaceKeepingWhitespace(translated, original)
    : original

  if (node.data !== nextValue)
    node.data = nextValue
}

function translateAttributes(element: Element) {
  if (shouldSkip(element)) return

  for (const attr of ['placeholder', 'title', 'aria-label']) {
    const value = element.getAttribute(attr)
    if (!value) continue

    let originals = attrOriginals.get(element)
    if (!originals) {
      originals = new Map()
      attrOriginals.set(element, originals)
    }

    const original = originals.get(attr) ?? value
    if (!originals.has(attr)) originals.set(attr, original)

    const translated = language.value === 'en'
      ? enText[original.trim()]
      : undefined

    element.setAttribute(attr, translated ?? original)
  }
}

function walk(root: Node) {
  if (root.nodeType === Node.TEXT_NODE) {
    translateTextNode(root as Text)
    return
  }

  if (root.nodeType !== Node.ELEMENT_NODE && root.nodeType !== Node.DOCUMENT_NODE)
    return

  if (root.nodeType === Node.ELEMENT_NODE)
    translateAttributes(root as Element)

  const walker = document.createTreeWalker(
    root,
    NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT
  )

  while (walker.nextNode()) {
    const node = walker.currentNode
    if (node.nodeType === Node.TEXT_NODE)
      translateTextNode(node as Text)
    else if (node.nodeType === Node.ELEMENT_NODE)
      translateAttributes(node as Element)
  }
}

export async function applyPageTranslations() {
  await nextTick()
  window.setTimeout(() => {
    applying = true
    walk(document.body)
    applying = false
  })
}

export function startTranslationObserver() {
  if (observer) return

  observer = new MutationObserver((mutations) => {
    if (applying) return

    applying = true
    for (const mutation of mutations) {
      for (const node of Array.from(mutation.addedNodes))
        walk(node)

      if (mutation.type === 'characterData')
        walk(mutation.target)

      if (mutation.type === 'attributes')
        walk(mutation.target)
    }
    applying = false
  })

  observer.observe(document.body, {
    subtree: true,
    childList: true,
    characterData: true,
    attributes: true,
    attributeFilter: ['placeholder', 'title', 'aria-label']
  })
}
