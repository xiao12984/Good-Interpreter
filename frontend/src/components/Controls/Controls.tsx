import { Mic, MonitorSpeaker, Square, Trash2, RefreshCw, Volume2, VolumeX } from 'lucide-react';
import { motion } from 'framer-motion';
import type { AudioInputMode, MicrophoneDevice } from '../../types';
import { VolumeVisualizer } from '../VolumeVisualizer';
import './Controls.css';

interface ControlsProps {
    isRecording: boolean;
    audioInputMode: AudioInputMode;
    microphones: MicrophoneDevice[];
    selectedMicrophoneId: string | null;
    volume: number;
    frequencyData: Uint8Array | null;
    isMuted: boolean;
    onStart: () => void;
    onStop: () => void;
    onClear: () => void;
    onAudioInputModeChange: (mode: AudioInputMode) => void;
    onMicrophoneChange: (deviceId: string) => void;
    onRefreshMicrophones: () => void;
    onToggleMute: () => void;
}

export function Controls({
    isRecording,
    audioInputMode,
    microphones,
    selectedMicrophoneId,
    volume,
    frequencyData,
    isMuted,
    onStart,
    onStop,
    onClear,
    onAudioInputModeChange,
    onMicrophoneChange,
    onRefreshMicrophones,
    onToggleMute,
}: ControlsProps) {
    const isSystemAudioMode = audioInputMode === 'system';

    return (
        <footer className="controls">
            {/* Audio input source */}
            <div className="audio-source-wrapper">
                <div className="audio-source-toggle" aria-label="音频输入来源">
                    <button
                        type="button"
                        className={`audio-source-btn ${audioInputMode === 'microphone' ? 'active' : ''}`}
                        onClick={() => onAudioInputModeChange('microphone')}
                        disabled={isRecording}
                        title="麦克风输入"
                    >
                        <Mic size={16} />
                        <span>麦克风</span>
                    </button>
                    <button
                        type="button"
                        className={`audio-source-btn ${isSystemAudioMode ? 'active' : ''}`}
                        onClick={() => onAudioInputModeChange('system')}
                        disabled={isRecording}
                        title="系统音频输入"
                    >
                        <MonitorSpeaker size={16} />
                        <span>系统音频</span>
                    </button>
                </div>
                <div className="mic-selector">
                    <span className="mic-selector-icon">
                        {isSystemAudioMode ? <MonitorSpeaker size={18} /> : <Mic size={18} />}
                    </span>
                    {isSystemAudioMode ? (
                        <span className="system-audio-label">浏览器选择音频来源</span>
                    ) : (
                        <select
                            className="mic-select"
                            value={selectedMicrophoneId || ''}
                            onChange={(e) => onMicrophoneChange(e.target.value)}
                            disabled={isRecording}
                        >
                            {microphones.length === 0 ? (
                                <option value="">请选择麦克风</option>
                            ) : (
                                microphones.map((mic) => (
                                    <option key={mic.deviceId} value={mic.deviceId}>
                                        {mic.label || `麦克风 ${mic.deviceId.slice(0, 8)}`}
                                    </option>
                                ))
                            )}
                        </select>
                    )}
                </div>
                <motion.button
                    className="mic-refresh-btn"
                    onClick={onRefreshMicrophones}
                    whileHover={{ scale: 1.1 }}
                    whileTap={{ scale: 0.95 }}
                    disabled={isRecording || isSystemAudioMode}
                    title="刷新麦克风列表"
                >
                    <RefreshCw size={18} />
                </motion.button>
            </div>

            {/* Translation Mode Display */}
            <div className="translation-mode">
                <span className="mode-badge">
                    🇨🇳 中文 ↔ en English
                </span>
            </div>

            {/* Volume Visualizer - Always visible */}
            <VolumeVisualizer volume={volume} frequencyData={frequencyData} isActive={isRecording} />

            {/* Action Buttons */}
            <div className="action-buttons">
                {!isRecording ? (
                    <motion.button
                        className="btn btn-primary btn-start"
                        onClick={onStart}
                        whileHover={{ scale: 1.05 }}
                        whileTap={{ scale: 0.95 }}
                    >
                        {isSystemAudioMode ? <MonitorSpeaker size={20} /> : <Mic size={20} />}
                        <span>开始翻译</span>
                    </motion.button>
                ) : (
                    <motion.button
                        className="btn btn-danger btn-stop"
                        onClick={onStop}
                        whileHover={{ scale: 1.05 }}
                        whileTap={{ scale: 0.95 }}
                    >
                        <Square size={20} />
                        <span>停止翻译</span>
                    </motion.button>
                )}

                <motion.button
                    className="btn btn-secondary btn-clear"
                    onClick={onClear}
                    whileHover={{ scale: 1.05 }}
                    whileTap={{ scale: 0.95 }}
                    title="清空记录"
                >
                    <Trash2 size={18} />
                </motion.button>

                <motion.button
                    className={`btn btn-secondary btn-mute ${isMuted ? 'muted' : ''}`}
                    onClick={onToggleMute}
                    whileHover={{ scale: 1.05 }}
                    whileTap={{ scale: 0.95 }}
                    title={isMuted ? '取消静音' : '静音朗读'}
                >
                    {isMuted ? <VolumeX size={18} /> : <Volume2 size={18} />}
                </motion.button>
            </div>
        </footer>
    );
}
