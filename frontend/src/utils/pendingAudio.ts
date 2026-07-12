import type { AudioInputMode } from '../types';

export const MAX_PENDING_AUDIO_CHUNKS = 240;

export function appendPendingAudioChunk(
    pendingAudio: string[],
    base64Data: string,
    audioInputMode: AudioInputMode,
    maxChunks = MAX_PENDING_AUDIO_CHUNKS
): string[] {
    const nextPendingAudio = [...pendingAudio, base64Data];

    if (nextPendingAudio.length <= maxChunks) {
        return nextPendingAudio;
    }

    if (audioInputMode !== 'microphone') {
        return nextPendingAudio.slice(-maxChunks);
    }

    // 麦克风模式第一个包是流式 WAV 头，溢出时保留头部，只丢弃最旧的音频数据包。
    return [
        nextPendingAudio[0],
        ...nextPendingAudio.slice(-(maxChunks - 1)),
    ];
}
