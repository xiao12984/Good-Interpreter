# Good-Interpreter Backend

这是 Good-Interpreter 的 Python 后端，基于 `aiohttp` 提供 REST API、翻译 WebSocket、字幕广播 WebSocket，并负责连接火山引擎 AST 2.0 和 OpenAI 总结接口。

## 目录

```text
backend/
├── app/
│   ├── main.py                    # aiohttp 应用入口
│   ├── config.py                  # 环境变量、源码/安装版路径、端口
│   ├── database.py                # SQLite 会话和消息存储
│   ├── routes/
│   │   ├── api.py                 # REST API
│   │   ├── websocket.py           # 翻译 WebSocket
│   │   └── captions.py            # 字幕浮窗只读 WebSocket
│   ├── services/
│   │   ├── bidirectional.py       # 中译英 / 英译中双 AST 会话
│   │   ├── caption_hub.py         # 字幕广播客户端集合
│   │   ├── language_direction.py  # ASR 语言和翻译方向判断
│   │   ├── summarizer.py          # OpenAI 会议总结
│   │   └── volcengine.py          # 单向 AST 服务封装
│   └── utils/
│       └── protobuf.py            # 火山 AST protobuf 构造和解析
├── ast_python/                    # 火山 AST proto 和生成模块
├── tests/                         # 后端轻量逻辑测试
├── run_backend.py                 # PyInstaller 入口
├── requirements.txt
└── README.md
```

## 配置

后端默认从 `backend/.env` 读取配置。默认端口是 `3100`。

```env
VOLC_APP_ID=your_app_id
VOLC_ACCESS_KEY=your_access_key
VOLC_RESOURCE_ID=volc.service_type.10053

OPENAI_API_KEY=sk-your_api_key

PORT=3100
HOST=0.0.0.0
DEBUG=false
```

`OPENAI_API_KEY` 只影响会议总结功能；实时同传只需要火山引擎配置。

## 本地启动

```powershell
cd backend
python -m venv venv
.\venv\Scripts\Activate
pip install -r requirements.txt
python -m app.main
```

启动后：

- HTTP：`http://localhost:3100`
- 翻译 WebSocket：`ws://localhost:3100/ws`
- 字幕浮窗 WebSocket：`ws://localhost:3100/ws/captions`

如果存在 `frontend/dist/index.html`，后端会托管前端静态页面；否则根路径只返回服务运行提示。

## WebSocket `/ws`

### 开始

```json
{
  "type": "start",
  "sourceLanguage": "zh",
  "targetLanguage": "en",
  "audioFormat": "wav"
}
```

`audioFormat` 可选值：

- `wav`：麦克风流，前端或 Launcher 会先发送流式 WAV 头
- `pcm`：系统音频，直接发送 16k 单声道 16-bit PCM

### 发送音频

```json
{
  "type": "audio",
  "data": "<base64_audio>"
}
```

### 停止

```json
{
  "type": "stop"
}
```

### 后端返回

| 类型 | 说明 |
| --- | --- |
| `sessionCreated` | 返回当前会议 `sessionId` |
| `status` | `ready` 等状态 |
| `asr` | 识别文本 |
| `translation` | 翻译文本 |
| `audio` | TTS 音频 |
| `sentenceComplete` | 当前句已完成并可落库 |
| `turnComplete` | AST 轮次结束 |
| `error` | 错误信息 |

## WebSocket `/ws/captions`

该通道给 Launcher 字幕浮窗使用，只读订阅后端广播。连接后会先收到 `idle` 状态；翻译过程中会收到：

```json
{
  "type": "caption",
  "sessionId": "...",
  "sourceText": "...",
  "targetText": "...",
  "sourceLanguage": "zh",
  "targetLanguage": "en",
  "isFinal": false
}
```

## REST API

| 路径 | 方法 | 说明 |
| --- | --- | --- |
| `/api/sessions?limit=10` | GET | 最近会话 |
| `/api/sessions/active` | GET | 当前活跃会话和消息 |
| `/api/sessions/{sessionId}` | GET | 指定会话和消息 |
| `/api/sessions/{sessionId}` | PATCH | 修改会话标题 |
| `/api/summarize` | POST | AI 会议总结 |

## 数据目录

源码运行时，数据库默认在：

```text
backend/data/translations.db
```

安装版运行时，数据库位于安装目录下的：

```text
backend/data/translations.db
```

## 测试

后端目前提供方向判断的标准库测试：

```powershell
python -m unittest backend.tests.test_language_direction
```

项目协作规则要求编译、运行和测试由维护者手动执行。
