# Good-Interpreter

Good-Interpreter 是一个中文和英文双向同声传译工具。项目包含 Python 后端、React Web 前端、Windows WinForms 启动器，以及面向安装包的一键打包脚本。

## 功能

| 模块 | 说明 |
| --- | --- |
| 实时同传 | 基于火山引擎 AST 2.0，同时建立中译英和英译中会话 |
| 音频输入 | Web 端支持麦克风和浏览器系统音频；Launcher 支持麦克风和 Windows 系统音频 |
| 字幕显示 | Web 端显示实时字幕和历史记录；Launcher 提供独立置顶字幕浮窗 |
| 语音播报 | 接收火山引擎 TTS 音频，可在 Web 端静音 |
| 会议记录 | SQLite 保存会话和翻译消息，支持导出文本记录 |
| AI 总结 | 可选 OpenAI Key，用于会议总结 |
| 安装包 | Inno Setup + PyInstaller + dotnet publish 生成 Windows 安装包 |

## 项目结构

```text
Good-Interpreter/
├── backend/
│   ├── app/
│   │   ├── main.py                  # aiohttp 应用入口
│   │   ├── config.py                # 环境变量、安装路径、端口配置
│   │   ├── database.py              # SQLite 会话和消息存储
│   │   ├── routes/
│   │   │   ├── api.py               # REST API
│   │   │   ├── websocket.py         # 翻译 WebSocket
│   │   │   └── captions.py          # Launcher 字幕浮窗只读 WebSocket
│   │   ├── services/
│   │   │   ├── bidirectional.py     # 双向 AST 会话管理
│   │   │   ├── caption_hub.py       # 字幕广播
│   │   │   ├── language_direction.py# 中英文方向判断
│   │   │   ├── summarizer.py        # OpenAI 会议总结
│   │   │   └── volcengine.py        # 单向 AST 服务封装
│   │   └── utils/
│   ├── ast_python/                  # 火山 AST protobuf 资源
│   ├── tests/                       # 后端轻量逻辑测试
│   ├── run_backend.py               # PyInstaller 后端入口
│   └── requirements.txt
├── frontend/
│   ├── src/
│   │   ├── components/              # Web UI 组件
│   │   ├── hooks/                   # 录音、播放、WebSocket
│   │   ├── services/                # REST API 调用
│   │   ├── types/                   # TypeScript 类型
│   │   └── utils/                   # 音频、字幕、音频队列工具
│   ├── tests/                       # 前端纯逻辑测试
│   ├── vite.config.ts               # 开发代理指向后端 3100
│   └── package.json
├── launcher/GoodInterpreter.Launcher/
│   ├── Controllers/                 # WinForms 窗口和应用上下文
│   ├── Services/                    # 后端启动、音频采集、WebSocket、导出总结
│   ├── Models/
│   ├── Config/
│   └── GoodInterpreter.Launcher.csproj
├── installer/
│   ├── package.ps1                  # 一键打包脚本
│   └── GoodInterpreter.iss          # Inno Setup 脚本
└── README.md
```

## 运行环境

- Node.js 20.19 或更高版本
- Python 3.9 或更高版本
- 火山引擎 AST App ID、Access Key
- 可选：OpenAI API Key，用于会议总结
- 可选：.NET 8 SDK、PyInstaller、Inno Setup 6，用于打包 Windows 安装包

## 配置

后端默认端口是 `3100`。源码运行和安装版都优先读取 `backend/.env`。

```env
VOLC_APP_ID=your_app_id
VOLC_ACCESS_KEY=your_access_key
VOLC_RESOURCE_ID=volc.service_type.10053

OPENAI_API_KEY=sk-your_api_key

PORT=3100
HOST=0.0.0.0
DEBUG=false
```

## 源码运行

### 后端

```powershell
cd backend
python -m venv venv
.\venv\Scripts\Activate
pip install -r requirements.txt
python -m app.main
```

后端启动后：

- Web 页面托管地址：`http://localhost:3100`
- 翻译 WebSocket：`ws://localhost:3100/ws`
- 字幕浮窗 WebSocket：`ws://localhost:3100/ws/captions`

### 前端

```powershell
cd frontend
npm install
npm run dev
```

开发地址是 `http://localhost:5173`。Vite 会把 `/api`、`/ws` 代理到 `localhost:3100`。

### Launcher

Launcher 是 `launcher/GoodInterpreter.Launcher` 下的 .NET 8 WinForms 项目。它负责：

- 保存后端 `.env` 配置
- 启动打包后的 `GoodInterpreter.Backend.exe`
- 打开 Web 前端
- 打开独立字幕浮窗
- 原生采集麦克风或系统音频并发送到后端

源码调试时需要确保仓库根目录包含 `backend` 和 `frontend`，Launcher 会从当前运行目录向上查找安装根目录。

## 打包

Windows 安装包由 `installer/package.ps1` 编排：

```powershell
cd installer
.\package.ps1
```

脚本会依次执行：

1. 检查 dotnet、node、npm、Python、PyInstaller、Inno Setup
2. 构建 `frontend/dist`
3. 使用 PyInstaller 生成 `GoodInterpreter.Backend.exe`
4. 使用 dotnet publish 生成 `GoodInterpreter.Launcher.exe`
5. 使用 Inno Setup 生成安装包

生成目录和编译产物不应提交到 Git；`.gitignore` 已忽略 `installer/build/`、`launcher/**/bin/`、`launcher/**/obj/`、`.codegraph/` 等本地输出。

## API 和通信

```text
Web 前端 / Launcher
        │
        ├── /ws             翻译控制和音频流
        ├── /ws/captions    字幕浮窗只读广播
        └── /api            会话、历史、总结
                │
          Python 后端
                │
          火山引擎 AST 2.0
```

常用接口：

| 路径 | 类型 | 说明 |
| --- | --- | --- |
| `/ws` | WebSocket | `start`、`audio`、`stop`，返回 ASR、翻译、TTS、状态 |
| `/ws/captions` | WebSocket | Launcher 字幕浮窗只读订阅 |
| `/api/sessions` | GET | 最近会话 |
| `/api/sessions/active` | GET | 当前活跃会话和消息 |
| `/api/sessions/{sessionId}` | GET/PATCH | 读取会话或修改标题 |
| `/api/summarize` | POST | AI 会议总结 |

## 检查

项目目前提供轻量逻辑测试入口。按项目规则，是否执行由维护者手动决定。

```powershell
# 后端方向判断逻辑
python -m unittest backend.tests.test_language_direction

# 前端音频队列和字幕合并逻辑
cd frontend
npm run test:logic
```

## Git 注意事项

不要提交以下内容：

- `backend/.env`
- `frontend/dist/`
- `installer/build/`
- `launcher/**/bin/`
- `launcher/**/obj/`
- `.codegraph/`
- `.vs/`、`.suo`、`.user`

## License

MIT
