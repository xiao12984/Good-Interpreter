import { useCallback, useEffect, useRef, useState } from 'react';
import type { AudioInputMode, ConnectionStatus, SubtitleItem } from '../types';
import { generateId } from '../utils/audio';
import { appendMergedSubtitle } from '../utils/subtitles';
import { appendPendingAudioChunk } from '../utils/pendingAudio';
import { useAudioPlayer } from './useAudioPlayer';
import { getActiveSession, messagesToSubtitles } from '../services/api';

interface UseWebSocketProps {
    sourceLanguage: string;
    targetLanguage: string;
}

interface UseWebSocketReturn {
    status: ConnectionStatus;
    sessionId: string | null;
    currentAsr: { text: string; isFinal: boolean };
    currentTranslation: { text: string; isFinal: boolean };
    subtitles: SubtitleItem[];
    lastError: string | null;
    isMuted: boolean;
    connect: (audioInputMode?: AudioInputMode) => Promise<void>;
    disconnect: () => void;
    sendAudio: (base64Data: string, audioInputMode?: AudioInputMode) => void;
    sendStop: () => void;
    clearSubtitles: () => void;
    loadHistory: () => Promise<void>;
    toggleMute: () => void;
}

type ReadyWaiter = {
    resolve: () => void;
    reject: (error: Error) => void;
    timeoutId: number;
};

export function useWebSocket({
    sourceLanguage,
    targetLanguage,
}: UseWebSocketProps): UseWebSocketReturn {
    const [status, setStatus] = useState<ConnectionStatus>('disconnected');
    const [sessionId, setSessionId] = useState<string | null>(null);
    const [currentAsr, setCurrentAsr] = useState({ text: '', isFinal: false });
    const [currentTranslation, setCurrentTranslation] = useState({
        text: '',
        isFinal: false,
    });
    const [subtitles, setSubtitles] = useState<SubtitleItem[]>([]);
    const [lastError, setLastError] = useState<string | null>(null);

    const wsRef = useRef<WebSocket | null>(null);
    const connectPromiseRef = useRef<Promise<void> | null>(null);
    const readyWaiterRef = useRef<ReadyWaiter | null>(null);
    const audioReadyRef = useRef(false);
    const audioInputModeRef = useRef<AudioInputMode>('microphone');
    const pendingAudioRef = useRef<string[]>([]);
    const currentAsrRef = useRef('');
    const currentTranslationRef = useRef('');
    const languagesRef = useRef({ source: sourceLanguage, target: targetLanguage });

    const { queueAudio, playQueuedAudio, stopPlayback, isMuted, toggleMute } = useAudioPlayer();

    const flushPendingAudio = useCallback(() => {
        const ws = wsRef.current;

        if (!ws || ws.readyState !== WebSocket.OPEN || !audioReadyRef.current) return;

        const pendingAudio = pendingAudioRef.current.splice(0);

        pendingAudio.forEach((base64Data) => {
            ws.send(
                JSON.stringify({
                    type: 'audio',
                    data: base64Data,
                })
            );
        });
    }, []);

    const resolveReadyWaiter = useCallback(() => {
        const waiter = readyWaiterRef.current;
        if (!waiter) return;

        window.clearTimeout(waiter.timeoutId);
        readyWaiterRef.current = null;
        connectPromiseRef.current = null;
        waiter.resolve();
    }, []);

    const rejectReadyWaiter = useCallback((message: string) => {
        const waiter = readyWaiterRef.current;
        if (!waiter) return;

        window.clearTimeout(waiter.timeoutId);
        readyWaiterRef.current = null;
        connectPromiseRef.current = null;
        waiter.reject(new Error(message));
    }, []);

    // Update language refs
    useEffect(() => {
        languagesRef.current = { source: sourceLanguage, target: targetLanguage };
    }, [sourceLanguage, targetLanguage]);

    // Load history on mount
    const loadHistory = useCallback(async () => {
        try {
            const { session, messages } = await getActiveSession();
            if (session && messages.length > 0) {
                setSessionId(session.sessionId);
                setSubtitles(messagesToSubtitles(messages));
                console.log(`Loaded ${messages.length} messages from history`);
            }
        } catch (error) {
            console.error('Failed to load history:', error);
        }
    }, []);

    // Load history on mount
    useEffect(() => {
        loadHistory();
    }, [loadHistory]);

    const addSubtitle = useCallback((sourceLang?: string, targetLang?: string) => {
        if (currentAsrRef.current || currentTranslationRef.current) {
            const newSubtitle: SubtitleItem = {
                id: generateId(),
                timestamp: new Date(),
                sourceText: currentAsrRef.current,
                targetText: currentTranslationRef.current,
                sourceLanguage: sourceLang || languagesRef.current.source,
                targetLanguage: targetLang || languagesRef.current.target,
            };
            setSubtitles((prev) => appendMergedSubtitle(prev, newSubtitle));
        }

        // Reset current texts
        currentAsrRef.current = '';
        currentTranslationRef.current = '';
        setCurrentAsr({ text: '', isFinal: false });
        setCurrentTranslation({ text: '', isFinal: false });
    }, []);

    const handleTurnComplete = useCallback(async () => {
        addSubtitle();
        // Play all queued audio after user finishes speaking
        await playQueuedAudio();
    }, [addSubtitle, playQueuedAudio]);

    const connect = useCallback((audioInputMode: AudioInputMode = 'microphone') => {
        if (wsRef.current?.readyState === WebSocket.OPEN && status === 'connected') {
            return Promise.resolve();
        }

        if (connectPromiseRef.current) {
            return connectPromiseRef.current;
        }

        setStatus('connecting');
        setLastError(null);
        audioReadyRef.current = false;
        audioInputModeRef.current = audioInputMode;

        if (wsRef.current && wsRef.current.readyState !== WebSocket.CLOSED) {
            wsRef.current.close();
            wsRef.current = null;
        }

        const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const ws = new WebSocket(`${wsProtocol}//${window.location.host}/ws`);
        wsRef.current = ws;

        const connectPromise = new Promise<void>((resolve, reject) => {
            const timeoutId = window.setTimeout(() => {
                if (wsRef.current === ws) {
                    ws.close();
                    wsRef.current = null;
                }

                audioReadyRef.current = false;
                setLastError('等待翻译服务就绪超时，请检查后端服务和火山 AST 连接。');
                setStatus('error');
                readyWaiterRef.current = null;
                connectPromiseRef.current = null;
                reject(new Error('等待翻译服务就绪超时，请检查后端服务和火山 AST 连接。'));
            }, 15000);

            readyWaiterRef.current = { resolve, reject, timeoutId };
        });

        connectPromiseRef.current = connectPromise;

        ws.onopen = () => {
            console.log('WebSocket connected');

            // Send start message
            ws.send(
                JSON.stringify({
                    type: 'start',
                    sourceLanguage: languagesRef.current.source,
                    targetLanguage: languagesRef.current.target,
                    audioFormat: audioInputMode === 'system' ? 'pcm' : 'wav',
                })
            );
        };

        ws.onmessage = (event) => {
            const data = JSON.parse(event.data);

            switch (data.type) {
                case 'sessionCreated':
                    setSessionId(data.sessionId);
                    console.log('Session created:', data.sessionId);
                    break;

                case 'status':
                    if (data.status === 'ready') {
                        setStatus('connected');
                        setLastError(null);
                        audioReadyRef.current = true;
                        flushPendingAudio();
                        resolveReadyWaiter();
                    } else if (data.status === 'disconnected') {
                        setStatus('disconnected');
                    }
                    break;

                case 'asr':
                    currentAsrRef.current = data.text || '';
                    setCurrentAsr({
                        text: data.text || '',
                        isFinal: data.isFinal,
                    });
                    break;

                case 'translation':
                    currentTranslationRef.current = data.text || '';
                    setCurrentTranslation({
                        text: data.text || '',
                        isFinal: data.isFinal,
                    });
                    break;

                case 'audio':
                    // Queue audio instead of playing immediately
                    queueAudio(data.data);
                    break;

                case 'sentenceComplete':
                    // Add subtitle with language info from backend and play queued audio
                    addSubtitle(data.sourceLanguage, data.targetLanguage);
                    playQueuedAudio();
                    break;

                case 'turnComplete':
                    handleTurnComplete();
                    break;

                case 'error':
                    console.error('Server error:', data.message);
                    setLastError(data.message || '服务器返回未知错误');
                    setStatus('error');
                    audioReadyRef.current = false;
                    rejectReadyWaiter(data.message || '服务器返回未知错误');
                    break;
            }
        };

        ws.onclose = () => {
            console.log('WebSocket disconnected');
            if (wsRef.current === ws) {
                wsRef.current = null;
            }
            setStatus('disconnected');
            audioReadyRef.current = false;
            rejectReadyWaiter('WebSocket 已断开，翻译服务没有进入就绪状态。');
        };

        ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            const message = 'WebSocket 连接异常，请检查后端服务是否正常。';
            setLastError(message);
            setStatus('error');
            audioReadyRef.current = false;
            rejectReadyWaiter(message);
        };

        return connectPromise;
    }, [
        status,
        handleTurnComplete,
        queueAudio,
        playQueuedAudio,
        addSubtitle,
        flushPendingAudio,
        resolveReadyWaiter,
        rejectReadyWaiter,
    ]);

    const disconnect = useCallback(() => {
        rejectReadyWaiter('已断开翻译连接。');
        audioReadyRef.current = false;
        pendingAudioRef.current = [];

        if (wsRef.current) {
            wsRef.current.close();
            wsRef.current = null;
        }
        stopPlayback();
        setStatus('disconnected');
        setLastError(null);
    }, [stopPlayback, rejectReadyWaiter]);

    const sendAudio = useCallback((base64Data: string, audioInputMode?: AudioInputMode) => {
        if (audioInputMode) {
            audioInputModeRef.current = audioInputMode;
        }

        if (wsRef.current?.readyState === WebSocket.OPEN && audioReadyRef.current) {
            wsRef.current?.send(
                JSON.stringify({
                    type: 'audio',
                    data: base64Data,
                })
            );
            return;
        }

        pendingAudioRef.current = appendPendingAudioChunk(
            pendingAudioRef.current,
            base64Data,
            audioInputModeRef.current
        );
    }, []);

    const sendStop = useCallback(() => {
        const ws = wsRef.current;
        wsRef.current = null;
        audioReadyRef.current = false;
        pendingAudioRef.current = [];
        rejectReadyWaiter('已停止翻译连接。');

        if (ws?.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type: 'stop' }));

            window.setTimeout(() => {
                ws.close();
                setStatus('disconnected');
            }, 300);
        } else if (ws?.readyState === WebSocket.CONNECTING) {
            ws.close();
            setStatus('disconnected');
        } else {
            setStatus('disconnected');
        }
    }, [rejectReadyWaiter]);

    const clearSubtitles = useCallback(() => {
        setSubtitles([]);
        currentAsrRef.current = '';
        currentTranslationRef.current = '';
        setCurrentAsr({ text: '', isFinal: false });
        setCurrentTranslation({ text: '', isFinal: false });
    }, []);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            disconnect();
        };
    }, [disconnect]);

    return {
        status,
        sessionId,
        currentAsr,
        currentTranslation,
        subtitles,
        lastError,
        isMuted,
        connect,
        disconnect,
        sendAudio,
        sendStop,
        clearSubtitles,
        loadHistory,
        toggleMute,
    };
}
