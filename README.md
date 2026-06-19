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
- **首次启动初始化向导**：首次部署时通过页面创建管理员账号，不再内置默认密码
- **操作日志**：记录登录、通道、配置、备份恢复、Wake-on-LAN 等关键操作
- **健康检查**：检查数据库和 frpc Web 管理接口状态，便于部署后排障
- **配置备份 / 恢复**：支持导出通道与 frpc 配置，并从备份文件恢复
- **Docker Compose**：提供 `docker-compose.yml`，可一条命令构建并启动容器
- **仪表板**：统计卡片 + 活动通道列表 + 服务器信息一览

---

## 技术栈

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3 · Vite · TypeScript · Element Plus · Pinia · Vue Router |
| 后端 | ASP.NET Core 8 · EF Core · JWT Bearer |
| 数据库 | SQLite（默认）· MySQL（可选，通过环境变量切换） |
| 通信 | frpc 内置 Web API（默认 `http://127.0.0.1:7400`） |

---

## 目录结构

```
frpcManager/
├── backend/
│   └── FrpcManager.Api/          # ASP.NET Core 8 Web API
│       ├── Controllers/           # Auth · Proxies · Config · AuditLogs · Backup · Health
│       ├── Services/              # 业务逻辑 + frpc API 集成 + TOML 解析
│       ├── Models/ & DTOs/        # 数据模型与传输对象
│       ├── Data/                  # EF Core SQLite 上下文
│       └── Program.cs
├── frontend/
│   └── src/
│       ├── views/                 # Login · Setup · Dashboard · Proxies · Settings · AuditLogs
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

> **首次初始化**：系统不再内置默认密码。首次打开页面时会自动进入初始化向导，创建第一个管理员账号。也可以通过 `Admin__Username` / `Admin__Password` 环境变量预置初始管理员，建议使用强密码。

---

## 运维功能说明

### 首次启动初始化向导

服务启动后会检查用户表是否为空。如果没有任何用户，前端会自动跳转到 `/setup`，要求创建第一个管理员账号；初始化完成后再进入登录页。

如需在无人值守部署中预置管理员，可以设置环境变量：

```bash
Admin__Username=admin
Admin__Password=请改成强密码
```

未设置 `Admin__Password` 时，系统不会自动创建默认管理员，避免暴露默认密码。

### 操作日志

左侧菜单提供“操作日志”页面，用于查看最近的关键操作记录，包括：

- 登录成功 / 失败
- 首次创建管理员
- 通道新增、修改、删除、启用、停用、同步
- frpc 配置更新、reload
- 配置备份导出、恢复
- Wake-on-LAN 发送

### 健康检查

系统设置页面提供健康检查卡片，也可以直接访问接口：

```bash
GET /api/health
```

返回内容包含数据库状态、frpc Web 管理接口状态和检查时间，适合用于部署后验证或外部监控探活。

### 配置备份 / 恢复

系统设置页面提供配置备份和恢复入口：

- 导出：下载包含通道列表和当前 frpc 配置的 JSON 备份文件
- 恢复：上传备份 JSON，可恢复通道配置，并可同步写回 frpc 配置

恢复操作会记录操作日志。生产环境建议在大批量修改通道前先导出备份。

### Docker Compose

项目根目录提供 `docker-compose.yml`，可以直接构建并启动：

```bash
docker compose up -d --build
```

默认映射端口：

| 宿主机端口 | 容器服务 |
|------|------|
| `6887` | HTTP |
| `6888` | HTTPS |

Compose 默认将数据持久化到 `frpc-manager-data` 卷，并将容器内 frpc Web 管理地址设为 `host.docker.internal:7400`。如果你的 frpc 不在宿主机或端口不同，请覆盖 `Frpc__WebServerAddr` / `Frpc__WebServerPort` 或 `Frpc__ApiBaseUrl`。

---

## 数据库配置

系统支持 SQLite 和 MySQL 双数据库：

- 默认使用 SQLite，适合单机部署和轻量使用
- 设置 `Database__Provider=mysql` 后切换到 MySQL，适合多人使用、容器化或需要集中备份的生产环境

### SQLite（默认）

不需要额外配置。默认连接字符串：

```bash
Database__Provider=sqlite
ConnectionStrings__DefaultConnection="Data Source=frpcmanager.db"
```

Docker 部署建议持久化数据库文件：

```bash
ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db"
```

### MySQL

切换到 MySQL 时至少需要设置：

```bash
Database__Provider=mysql
ConnectionStrings__MySql="Server=127.0.0.1;Port=3306;Database=frpcmanager;User=frpcmanager;Password=请改成强密码;CharSet=utf8mb4;"
```

也可以把 MySQL 连接串写到 `ConnectionStrings__DefaultConnection`，系统会在未设置 `ConnectionStrings__MySql` 时回退使用它：

```bash
Database__Provider=mysql
ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;Database=frpcmanager;User=frpcmanager;Password=请改成强密码;CharSet=utf8mb4;"
```

默认按 MySQL 8.0 生成 EF Core 语句。如果使用其他兼容版本，可以覆盖：

```bash
Database__MySqlServerVersion=8.0.36
```

使用 Docker Compose 中的 MySQL 服务时，需要把 `frpc-manager` 服务的数据库环境变量改为：

```yaml
Database__Provider: mysql
ConnectionStrings__MySql: Server=mysql;Port=3306;Database=frpcmanager;User=frpcmanager;Password=change-this-password;CharSet=utf8mb4;
```

然后启动：

```bash
docker compose --profile mysql up -d --build
```

首次连接 MySQL 前请确认数据库用户、密码和库名已经创建，并且应用所在主机可以访问 MySQL 端口。

---

## API 说明

后端提供以下 API。除首次初始化状态、初始化创建管理员、健康检查等公开接口外，其余请求需携带 JWT Token：

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
| `GET` | `/api/auth/setup-status` | 获取是否需要首次初始化 |
| `POST` | `/api/auth/setup` | 首次启动时创建管理员账号 |
| `GET` | `/api/audit-logs` | 获取操作日志 |
| `GET` | `/api/backup` | 导出通道和 frpc 配置备份 |
| `POST` | `/api/backup/restore` | 恢复配置备份 |
| `GET` | `/api/health` | 健康检查 |

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

### Docker

```bash
# 构建镜像
docker build -t frpc-manager .

# 运行容器
docker run -d \
  --name frpc-manager \
  -p 6887:6887 \
  -p 6888:6888 \
  -v frpc-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db" \
  -e Admin__Password="请改成强密码" \
  frpc-manager
```

也可以使用 Docker Compose：

```bash
docker compose up -d --build
```

如果需要构建镜像并推送到 Docker Hub，项目根目录提供交互式脚本。脚本会要求输入 Docker Hub 用户名、镜像仓库名、Tag，并询问是否使用 `--no-cache` 强制构建。

Windows PowerShell / CMD：

```powershell
.\docker-build-push.bat
```

Linux：

```bash
chmod +x docker-build-push-linux.sh
./docker-build-push-linux.sh
```

推送完成后，请在 Docker Hub 仓库页面确认仓库可见性为 Public。`docker push` 只负责上传镜像，不会自动修改 Docker Hub 仓库公开/私有状态。

如果 frpc 运行在宿主机，容器内的 `127.0.0.1` 指向容器自身，需要按实际环境覆盖 frpc API 地址。例如 Docker Desktop：

```bash
docker run -d \
  --name frpc-manager \
  -p 6887:6887 \
  -p 6888:6888 \
  -v frpc-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db" \
  -e Admin__Password="请改成强密码" \
  -e Frpc__WebServerAddr="host.docker.internal" \
  -e Frpc__WebServerPort="7400" \
  frpc-manager
```

frpc Web 管理地址也可以直接用完整地址覆盖：

```bash
-e Frpc__ApiBaseUrl="http://host.docker.internal:7400"
```

配置优先级为：`Frpc__ApiBaseUrl` 优先；未设置时使用 `Frpc__WebServerAddr` 和 `Frpc__WebServerPort` 拼接。普通服务器默认 `127.0.0.1:7400`，Docker 镜像内默认 `host.docker.internal:7400`。

Linux 环境如果需要容器发送 Wake-on-LAN 广播包到局域网，建议使用 host 网络模式：

```bash
docker run -d \
  --name frpc-manager \
  --network host \
  -v frpc-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db" \
  -e Admin__Password="请改成强密码" \
  frpc-manager
```

使用 host 网络模式时不需要 `-p 6887:6887 -p 6888:6888`，服务会直接监听宿主机的 `6887` 和 `6888` 端口。

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
# 1. 构建前端
cd frontend
npm run build

# 2. 复制前端产物到后端 wwwroot
rm -rf ../backend/FrpcManager.Api/wwwroot
mkdir -p ../backend/FrpcManager.Api/wwwroot
cp -R dist/. ../backend/FrpcManager.Api/wwwroot/

# 3. 发布后端
cd ../backend/FrpcManager.Api
dotnet publish -c Release -o ./publish

# 4. 运行（前后端一体）
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
