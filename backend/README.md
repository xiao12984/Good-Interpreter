# 会议同声传译 - Python 后端

基于火山引擎 AST 2.0 API 的双向同声传译后端服务。

## 📁 项目结构

```
backend/
├── app/
│   ├── main.py              # 应用入口
│   ├── config.py            # 配置管理
│   ├── database.py          # SQLite 数据库
│   ├── routes/
│   │   ├── api.py           # REST API 路由
│   │   └── websocket.py     # WebSocket 路由
│   └── services/
│       ├── bidirectional.py # 双向翻译核心
│       ├── volcengine.py    # 火山引擎 ASR/TTS
│       └── summarizer.py    # AI 总结服务 (OpenAI)
├── requirements.txt
├── .env                     # 环境变量配置
├── start.sh                 # 启动脚本
└── README.md
```

## 🚀 快速开始

### 安装依赖

```bash
cd backend
python -m venv venv
source venv/bin/activate  # macOS/Linux
.\venv\Scripts\Activate  # Windows
pip install -r requirements.txt
```

### 配置环境变量

创建 `.env` 文件：

```env
# 火山引擎 AST API (必须)
VOLC_APP_ID=your_app_id
VOLC_ACCESS_KEY=your_access_key

# OpenAI API (AI总结功能)
OPENAI_API_KEY=sk-your_api_key

# 服务端口
PORT=3100
```

### 运行

```bash
./start.sh
# 或
source venv/bin/activate && python -m app.main

# 激活虚拟环境
.\venv\Scripts\Activate

# 运行 Python 后端
python -m app.main
```

服务启动后：
- HTTP: http://localhost:3100
- WebSocket: ws://localhost:3100/ws

## 📡 API 接口

### WebSocket `/ws`

#### 开始翻译
```json
{"type": "start", "sourceLanguage": "zh", "targetLanguage": "en"}
```

#### 发送音频
```json
{"type": "audio", "data": "<base64_pcm_audio>"}
```

#### 停止翻译
```json
{"type": "stop"}
```

### REST API

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/sessions` | GET | 获取历史会话 |
| `/api/sessions/active` | GET | 获取当前活跃会话 |
| `/api/sessions/{id}/messages` | GET | 获取会话消息 |
| `/api/summarize` | POST | AI 生成会议总结 |

### 响应消息类型

| 类型 | 说明 |
|------|------|
| `status` | 连接状态 |
| `asr` | 语音识别结果 |
| `translation` | 翻译结果 |
| `audio` | TTS 音频 (base64) |
| `sentenceComplete` | 句子翻译完成 |
| `turnComplete` | 轮次结束 |
| `error` | 错误信息 |

## 🔧 技术栈

- **aiohttp** - 异步 Web 框架
- **火山引擎 AST 2.0** - 语音识别/合成/翻译
- **OpenAI API** - AI 会议总结
- **SQLite** - 本地数据存储
