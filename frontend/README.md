# Good-Interpreter Frontend

这是 Good-Interpreter 的 React + TypeScript Web 前端。它负责浏览器端录音、系统音频捕获、WebSocket 通信、字幕展示、TTS 播放、会议记录导出和 AI 总结入口。

## 目录

```text
frontend/
├── src/
│   ├── App.tsx
│   ├── components/
│   │   ├── BackgroundEffects/
│   │   ├── Controls/              # 音频来源、设备、开始/停止、静音
│   │   ├── CurrentTranslation/
│   │   ├── Header/
│   │   ├── SubtitleDisplay/       # 实时字幕、历史记录、导出、总结
│   │   ├── VisualStage/           # 状态和音频可视化舞台
│   │   └── VolumeVisualizer/
│   ├── hooks/
│   │   ├── useAudioPlayer.ts      # TTS 播放队列
│   │   ├── useAudioRecorder.ts    # 麦克风/系统音频采集
│   │   └── useWebSocket.ts        # 翻译 WebSocket 状态机
│   ├── services/
│   │   └── api.ts                 # 会话、历史、总结 API
│   ├── types/
│   │   └── index.ts
│   └── utils/
│       ├── audio.ts               # PCM/Base64/时间工具
│       ├── pendingAudio.ts        # ready 前音频缓存策略
│       └── subtitles.ts           # 字幕合并规则
├── tests/                         # 纯逻辑测试
├── tsconfig.test.json
├── vite.config.ts
└── package.json
```

## 功能

- 中文和英文双向同声传译
- 麦克风输入
- 浏览器系统音频输入，适合翻译会议、视频或远程通话声音
- 翻译服务 ready 前缓存音频，ready 后再发送
- 实时字幕和历史字幕合并展示
- TTS 播放和静音控制
- 会议记录导出
- AI 会议总结

## 本地开发

后端默认需要运行在 `localhost:3100`。Vite 开发服务器会把 `/api` 和 `/ws` 代理过去。

```powershell
cd frontend
npm install
npm run dev
```

访问：

```text
http://localhost:5173
```

## 生产构建

```powershell
cd frontend
npm run build
```

构建产物在 `frontend/dist/`。生产模式下由 Python 后端托管，访问：

```text
http://localhost:3100
```

## 脚本

| 命令 | 说明 |
| --- | --- |
| `npm run dev` | 启动 Vite 开发服务器 |
| `npm run build` | TypeScript 检查并构建生产产物 |
| `npm run lint` | 运行 ESLint |
| `npm run test:logic` | 编译并运行前端纯逻辑测试 |
| `npm run preview` | 预览 Vite 生产构建 |

## 音频格式

WebSocket `start` 会根据音频来源发送 `audioFormat`：

- 麦克风：`wav`
- 系统音频：`pcm`

前端会把输入统一转换成 16k 单声道 16-bit PCM。麦克风模式会额外先发送流式 WAV 头；系统音频模式不会发送 WAV 头。

## 字幕合并

字幕历史会按以下规则合并：

- 同方向才合并，例如 `zh` 和 `zh-CN` 视为同类中文
- 中英文方向不同不合并
- 超过时间间隔不合并
- 上一句已有句号、问号、感叹号等硬结束符时不合并
- 合并后过长不合并

## 测试

前端测试只覆盖不依赖浏览器权限的纯逻辑：

```powershell
cd frontend
npm run test:logic
```

项目协作规则要求编译、运行和测试由维护者手动执行。
