import { useCallback, useEffect, useRef, useState } from 'react';
import type { AudioInputMode, MicrophoneDevice } from '../types';
import { arrayBufferToBase64, convertFloat32ToInt16 } from '../utils/audio';

interface UseAudioRecorderProps {
    onAudioData: (base64Data: string) => void;
    onVolumeChange?: (volume: number, frequencyData: Uint8Array) => void;
}

interface UseAudioRecorderReturn {
    isRecording: boolean;
    audioInputMode: AudioInputMode;
    microphones: MicrophoneDevice[];
    selectedMicrophoneId: string | null;
    startRecording: () => Promise<void>;
    stopRecording: () => void;
    selectAudioInputMode: (mode: AudioInputMode) => void;
    selectMicrophone: (deviceId: string) => void;
    refreshMicrophones: () => Promise<void>;
}

type SystemAudioDisplayMediaOptions = DisplayMediaStreamOptions & {
    systemAudio?: 'include' | 'exclude';
    surfaceSwitching?: 'include' | 'exclude';
    selfBrowserSurface?: 'include' | 'exclude';
};

const TARGET_SAMPLE_RATE = 16000;
const TARGET_CHANNEL_COUNT = 1;
const TARGET_BITS_PER_SAMPLE = 16;

function writeAscii(view: DataView, offset: number, text: string) {
    for (let index = 0; index < text.length; index++) {
        view.setUint8(offset + index, text.charCodeAt(index));
    }
}

function createStreamingWavHeader(): ArrayBuffer {
    const header = new ArrayBuffer(44);
    const view = new DataView(header);
    const byteRate = TARGET_SAMPLE_RATE * TARGET_CHANNEL_COUNT * TARGET_BITS_PER_SAMPLE / 8;
    const blockAlign = TARGET_CHANNEL_COUNT * TARGET_BITS_PER_SAMPLE / 8;

    writeAscii(view, 0, 'RIFF');
    view.setUint32(4, 0xffffffff, true);
    writeAscii(view, 8, 'WAVE');
    writeAscii(view, 12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, TARGET_CHANNEL_COUNT, true);
    view.setUint32(24, TARGET_SAMPLE_RATE, true);
    view.setUint32(28, byteRate, true);
    view.setUint16(32, blockAlign, true);
    view.setUint16(34, TARGET_BITS_PER_SAMPLE, true);
    writeAscii(view, 36, 'data');
    view.setUint32(40, 0xffffffff, true);

    return header;
}

function calculateChannelRms(channelData: Float32Array): number {
    let sum = 0;

    for (let sampleIndex = 0; sampleIndex < channelData.length; sampleIndex++) {
        sum += channelData[sampleIndex] * channelData[sampleIndex];
    }

    return Math.sqrt(sum / Math.max(channelData.length, 1));
}

function selectMonoChannelForAst(inputBuffer: AudioBuffer): Float32Array {
    const channelCount = inputBuffer.numberOfChannels;

    if (channelCount <= 1) {
        return inputBuffer.getChannelData(0);
    }

    const sampleCount = inputBuffer.length;
    let strongestChannelData = inputBuffer.getChannelData(0);
    let strongestRms = calculateChannelRms(strongestChannelData);

    // 系统音频常见多声道输入，直接平均可能让左右声道相位抵消，导致送到 AST 的语音变弱。
    for (let channelIndex = 1; channelIndex < channelCount; channelIndex++) {
        const channelData = inputBuffer.getChannelData(channelIndex);
        const channelRms = calculateChannelRms(channelData);

        if (channelRms > strongestRms) {
            strongestChannelData = channelData;
            strongestRms = channelRms;
        }
    }

    const monoData = new Float32Array(sampleCount);
    monoData.set(strongestChannelData);
    return monoData;
}

function resampleToTargetRate(inputData: Float32Array, inputSampleRate: number): Float32Array {
    if (inputSampleRate === TARGET_SAMPLE_RATE) {
        return inputData;
    }

    const ratio = inputSampleRate / TARGET_SAMPLE_RATE;
    const outputLength = Math.max(1, Math.round(inputData.length / ratio));
    const outputData = new Float32Array(outputLength);

    for (let outputIndex = 0; outputIndex < outputLength; outputIndex++) {
        const sourceIndex = outputIndex * ratio;
        const sourceIndexFloor = Math.floor(sourceIndex);
        const sourceIndexCeil = Math.min(sourceIndexFloor + 1, inputData.length - 1);
        const weight = sourceIndex - sourceIndexFloor;

        outputData[outputIndex] =
            inputData[sourceIndexFloor] * (1 - weight) +
            inputData[sourceIndexCeil] * weight;
    }

    return outputData;
}

export function useAudioRecorder({
    onAudioData,
    onVolumeChange,
}: UseAudioRecorderProps): UseAudioRecorderReturn {
    const [isRecording, setIsRecording] = useState(false);
    const [audioInputMode, setAudioInputMode] = useState<AudioInputMode>('microphone');
    const [microphones, setMicrophones] = useState<MicrophoneDevice[]>([]);
    const [selectedMicrophoneId, setSelectedMicrophoneId] = useState<string | null>(null);

    const audioContextRef = useRef<AudioContext | null>(null);
    const analyserRef = useRef<AnalyserNode | null>(null);
    const streamRef = useRef<MediaStream | null>(null);
    const animationFrameRef = useRef<number | null>(null);
    const initializedRef = useRef(false);
    const isRecordingRef = useRef(false);

    const refreshMicrophones = useCallback(async () => {
        try {
            // Request permission first
            await navigator.mediaDevices.getUserMedia({ audio: true });

            const devices = await navigator.mediaDevices.enumerateDevices();
            const audioInputs = devices
                .filter((device) => device.kind === 'audioinput')
                .map((device, index) => ({
                    deviceId: device.deviceId,
                    label: device.label || `麦克风 ${index + 1}`,
                }));

            setMicrophones(audioInputs);

            // Set default microphone
            if (audioInputs.length > 0 && !selectedMicrophoneId) {
                const defaultDevice =
                    audioInputs.find((d) => d.deviceId === 'default') || audioInputs[0];
                setSelectedMicrophoneId(defaultDevice.deviceId);
            }
        } catch (error) {
            console.error('Error getting microphone devices:', error);
        }
    }, [selectedMicrophoneId]);

    const selectMicrophone = useCallback((deviceId: string) => {
        setSelectedMicrophoneId(deviceId);
    }, []);

    const selectAudioInputMode = useCallback((mode: AudioInputMode) => {
        setAudioInputMode(mode);
    }, []);

    const createMicrophoneStream = useCallback(async () => {
        const audioConstraints: MediaTrackConstraints = {
            sampleRate: TARGET_SAMPLE_RATE,
            channelCount: TARGET_CHANNEL_COUNT,
            echoCancellation: true,
            noiseSuppression: true,
        };

        if (selectedMicrophoneId) {
            audioConstraints.deviceId = { exact: selectedMicrophoneId };
        }

        return navigator.mediaDevices.getUserMedia({
            audio: audioConstraints,
        });
    }, [selectedMicrophoneId]);

    const createSystemAudioStream = useCallback(async () => {
        if (!navigator.mediaDevices.getDisplayMedia) {
            throw new Error('当前浏览器不支持系统音频输入，请使用 Chrome 或 Edge。');
        }

        const displayOptions: SystemAudioDisplayMediaOptions = {
            video: true,
            audio: true,
            systemAudio: 'include',
            surfaceSwitching: 'include',
            selfBrowserSurface: 'exclude',
        };

        const displayStream = await navigator.mediaDevices.getDisplayMedia(displayOptions);

        const audioTracks = displayStream.getAudioTracks();

        if (audioTracks.length === 0) {
            displayStream.getTracks().forEach((track) => track.stop());
            throw new Error('没有捕获到系统音频，请在浏览器共享窗口中勾选音频。');
        }

        return displayStream;
    }, []);

    const createAudioStream = useCallback(() => {
        return audioInputMode === 'system'
            ? createSystemAudioStream()
            : createMicrophoneStream();
    }, [audioInputMode, createMicrophoneStream, createSystemAudioStream]);

    const startRecording = useCallback(async () => {
        try {
            const stream = await createAudioStream();
            streamRef.current = stream;

            const audioContext = new AudioContext({ sampleRate: TARGET_SAMPLE_RATE });
            audioContextRef.current = audioContext;

            // 系统音频捕获会同时带一个屏幕视频轨道，这里只把音频轨道送入处理链。
            const audioOnlyStream = new MediaStream(stream.getAudioTracks());
            const source = audioContext.createMediaStreamSource(audioOnlyStream);
            const processor = audioContext.createScriptProcessor(4096, 1, 1);

            // Create analyser for volume visualization
            const analyser = audioContext.createAnalyser();
            analyser.fftSize = 256;
            analyserRef.current = analyser;
            source.connect(analyser);

            processor.onaudioprocess = (e) => {
                // Use ref to get current recording state (avoids stale closure)
                if (!isRecordingRef.current) return;

                const monoData = selectMonoChannelForAst(e.inputBuffer);
                const resampledData = resampleToTargetRate(monoData, e.inputBuffer.sampleRate);
                const pcmData = convertFloat32ToInt16(resampledData);
                const base64Data = arrayBufferToBase64(pcmData.buffer as ArrayBuffer);

                onAudioData(base64Data);
            };

            source.connect(processor);
            processor.connect(audioContext.destination);

            isRecordingRef.current = true;
            setIsRecording(true);
            if (audioInputMode === 'microphone') {
                onAudioData(arrayBufferToBase64(createStreamingWavHeader()));
            }

            // Start volume visualization
            if (onVolumeChange) {
                const updateVolume = () => {
                    if (!analyserRef.current) return;

                    const dataArray = new Uint8Array(analyserRef.current.frequencyBinCount);
                    analyserRef.current.getByteFrequencyData(dataArray);

                    let sum = 0;
                    for (let i = 0; i < dataArray.length; i++) {
                        sum += dataArray[i];
                    }
                    const average = sum / dataArray.length;
                    // Use higher divisor (180) to make volume thresholds more accurate
                    const normalizedVolume = Math.min(average / 180, 1);

                    onVolumeChange(normalizedVolume, dataArray);

                    animationFrameRef.current = requestAnimationFrame(updateVolume);
                };
                updateVolume();
            }
        } catch (error) {
            console.error('Error starting recording:', error);
            throw error;
        }
    }, [audioInputMode, createAudioStream, onAudioData, onVolumeChange]);

    const stopRecording = useCallback(() => {
        isRecordingRef.current = false;
        setIsRecording(false);

        if (animationFrameRef.current) {
            cancelAnimationFrame(animationFrameRef.current);
            animationFrameRef.current = null;
        }

        if (audioContextRef.current) {
            audioContextRef.current.close();
            audioContextRef.current = null;
            analyserRef.current = null;
        }

        if (streamRef.current) {
            streamRef.current.getTracks().forEach((track) => track.stop());
            streamRef.current = null;
        }
    }, []);

    // Initialize microphones on mount
    useEffect(() => {
        if (initializedRef.current) return;
        initializedRef.current = true;

        // Use async IIFE to avoid returning a promise
        (async () => {
            try {
                await navigator.mediaDevices.getUserMedia({ audio: true });
                const devices = await navigator.mediaDevices.enumerateDevices();
                const audioInputs = devices
                    .filter((device) => device.kind === 'audioinput')
                    .map((device, index) => ({
                        deviceId: device.deviceId,
                        label: device.label || `麦克风 ${index + 1}`,
                    }));
                setMicrophones(audioInputs);
                if (audioInputs.length > 0) {
                    const defaultDevice = audioInputs.find((d) => d.deviceId === 'default') || audioInputs[0];
                    setSelectedMicrophoneId(defaultDevice.deviceId);
                }
            } catch (error) {
                console.error('Error getting microphone devices:', error);
            }
        })();
    }, []);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            stopRecording();
        };
    }, [stopRecording]);

    return {
        isRecording,
        audioInputMode,
        microphones,
        selectedMicrophoneId,
        startRecording,
        stopRecording,
        selectAudioInputMode,
        selectMicrophone,
        refreshMicrophones,
    };
}
