# FrpC 管理平台

基于 **Vue 3 + ASP.NET Core 8 + SQLite** 的 frpc 内网穿透 Web 管理平台，与 frpc 运行在同一主机上，通过可视化界面管理所有穿透通道。

---

## 功能特性

- **通道总览**：以表格形式展示所有 TCP / UDP 通道，实时显示运行状态
- **状态监控**：通过 frpc API 实时获取活动通道，绿点标记运行中的通道
- **一键启用 / 停用**：切换开关自动更新 `frpc.toml` 配置并触发 `frpc reload`
- **通道管理**：增加、编辑、删除通道，字段含完整说明
- **搜索 & 筛选**：按名称搜索，按协议类型（TCP / UDP）和状态过滤
- **从 frpc 同步**：一键导入 frpc 当前配置文件中的已有通道
- **服务器配置**：可视化修改 frpc 服务端地址、端口、认证 Token 等
- **用户认证**：JWT 登录鉴权，支持修改密码
- **仪表板**：统计卡片 + 活动通道列表 + 服务器信息一览

---

## 技术栈

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3 · Vite · TypeScript · Element Plus · Pinia · Vue Router |
| 后端 | ASP.NET Core 8 · EF Core · JWT Bearer |
| 数据库 | SQLite（自动创建，无需手动初始化） |
| 通信 | frpc 内置 Web API（默认 `http://127.0.0.1:7400`） |

---

## 目录结构

```
frpcManager/
├── backend/
│   └── FrpcManager.Api/          # ASP.NET Core 8 Web API
│       ├── Controllers/           # Auth · Proxies · Config
│       ├── Services/              # 业务逻辑 + frpc API 集成 + TOML 解析
│       ├── Models/ & DTOs/        # 数据模型与传输对象
│       ├── Data/                  # EF Core SQLite 上下文
│       └── Program.cs
├── frontend/
│   └── src/
│       ├── views/                 # Login · Dashboard · Proxies · Settings
│       ├── components/            # AppLayout · ProxyFormDialog
│       ├── stores/                # Pinia 状态管理
│       ├── api/                   # Axios 请求封装
│       └── router/
├── install.bat                    # 一键安装前端依赖
├── start-dev.bat                  # 一键启动开发环境
├── start-publish.bat              # Windows 一键拉取、构建、发布并启动
├── start-publish-linux.bat        # Linux 一键拉取、构建、发布并启动
└── 需求.md
```

---

## 快速开始

### 前置条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- 同主机已安装并运行 frpc，且 `webServer` 已启用（见下方配置示例）

### frpc 配置要求

确保 `frpc.toml` 中开启了 Web 管理接口：

```toml
serverAddr = "your.server.com"
serverPort = 7000

auth.method = "token"
auth.token  = "your_token"

webServer.addr = "127.0.0.1"
webServer.port = 7400
```

### 安装 & 启动

```bash
# 1. 克隆仓库
git clone https://github.com/fzj929/frpcManager.git
cd frpcManager

# 2. 安装前端依赖（首次运行）
install.bat

# 3. 启动开发环境
start-dev.bat
```

启动后访问：

| 地址 | 说明 |
|------|------|
| http://localhost:5173 | 前端管理界面 |
| http://localhost:6887 | 后端 API（HTTP） |
| https://localhost:6888 | 后端 API（HTTPS） |
| https://localhost:6888/swagger | Swagger 接口文档 |

> **默认账号**：`admin` / `admin123`（首次登录后请及时修改密码）

---

## API 说明

后端代理了以下 frpc 原生 API，所有请求需携带 JWT Token：

| 方法 | 路径 | 说明 |
|------|------|------|
| `POST` | `/api/auth/login` | 用户登录，返回 JWT |
| `POST` | `/api/auth/change-password` | 修改密码 |
| `GET` | `/api/proxies` | 获取所有通道（含运行状态） |
| `POST` | `/api/proxies` | 添加通道 |
| `PUT` | `/api/proxies/{id}` | 编辑通道 |
| `DELETE` | `/api/proxies/{id}` | 删除通道 |
| `PUT` | `/api/proxies/{id}/enable` | 启用通道（写入 frpc 并 reload） |
| `PUT` | `/api/proxies/{id}/disable` | 停用通道（从 frpc 移除并 reload） |
| `POST` | `/api/proxies/sync` | 从 frpc 当前配置同步通道 |
| `GET` | `/api/config` | 获取 frpc 服务器配置 |
| `PUT` | `/api/config` | 更新 frpc 服务器配置并 reload |
| `GET` | `/api/config/status` | 获取 frpc 实时通道状态 |
| `POST` | `/api/config/reload` | 手动触发 frpc reload |
| `POST` | `/api/wake-on-lan` | 根据 MAC 地址发送 Wake-on-LAN 魔术数据包 |

---

## 通道字段说明

| 字段 | 说明 | 示例 |
|------|------|------|
| 通道名称 | 唯一标识，建议英文+数字 | `rdp-office` |
| 协议类型 | TCP（可靠传输）/ UDP（低延迟） | `tcp` |
| 本地 IP | 内网目标机器的 IP 地址 | `192.168.0.100` |
| 本地端口 | 目标机器上的服务端口 | `3389`（RDP） |
| 远程端口 | frp 服务器对外开放的端口 | `6001` |
| 描述 | 可选备注 | `办公室电脑远程桌面` |

---

## 生产部署

推荐使用发布启动脚本自动完成拉取代码、前端构建、后端发布和启动。

### Windows

```powershell
# 默认会先执行 git pull
.\start-publish.bat

# 网络不可用或不需要拉取代码时跳过 git pull
.\start-publish.bat --no-pull
```

### Linux

```bash
chmod +x start-publish-linux.bat

# 默认会先执行 git pull
./start-publish-linux.bat

# 网络不可用或不需要拉取代码时跳过 git pull
./start-publish-linux.bat --no-pull
```

启动后访问：

| 地址 | 说明 |
|------|------|
| http://localhost:6887 | 后端 API（HTTP） |
| https://localhost:6888 | 后端 API（HTTPS，自签名证书） |
| https://localhost:6888/swagger | Swagger 接口文档 |

也可以手动执行发布流程：

```bash
# 1. 构建前端（输出到后端 wwwroot）
cd frontend
npm run build

# 2. 发布后端
cd ../backend/FrpcManager.Api
dotnet publish -c Release -o ./publish

# 3. 运行（前后端一体）
cd publish
dotnet FrpcManager.Api.dll
# 访问 http://0.0.0.0:6887 或 https://0.0.0.0:6888
```

---

## 截图预览

| 仪表板 | 通道管理 |
|--------|---------|
| 统计卡片 + 服务器信息 + 活动通道列表 | 通道列表 + 状态指示灯 + 一键启用开关 |

| 添加通道 | 系统设置 |
|---------|---------|
| 含字段说明的表单对话框 | frpc 配置 + 修改密码 |

---

## License

MIT
