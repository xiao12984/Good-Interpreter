import { useEffect, useMemo, useRef, type CSSProperties } from 'react';
import { Activity, Languages, Mic2, Volume2, VolumeX } from 'lucide-react';
import { motion } from 'framer-motion';
import type { ConnectionStatus } from '../../types';
import './VisualStage.css';

interface VisualStageProps {
    status: ConnectionStatus;
    volume: number;
    frequencyData: Uint8Array | null;
    currentSourceText: string;
    currentTargetText: string;
    subtitleCount: number;
    isMuted: boolean;
    errorMessage?: string | null;
}

const statusText: Record<ConnectionStatus, string> = {
    disconnected: '待机',
    connecting: '连接中',
    connected: '已就绪',
    recording: '监听中',
    error: '异常',
};

export function VisualStage({
    status,
    volume,
    frequencyData,
    currentSourceText,
    currentTargetText,
    subtitleCount,
    isMuted,
    errorMessage,
}: VisualStageProps) {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const animationRef = useRef<number>(0);
    const statusRef = useRef(status);
    const volumeRef = useRef(volume);
    const frequencyDataRef = useRef<Uint8Array | null>(frequencyData);

    const volumePercent = Math.round(volume * 100);
    const hasSpeech = currentSourceText.trim().length > 0;
    const hasTranslation = currentTargetText.trim().length > 0;

    const stageItems = useMemo(
        () => [
            {
                icon: <Mic2 size={18} />,
                label: '输入',
                value: hasSpeech ? '识别中' : status === 'recording' ? '等待语音' : '未开始',
                active: status === 'recording',
            },
            {
                icon: <Languages size={18} />,
                label: '翻译',
                value: hasTranslation ? '输出中' : '待处理',
                active: hasTranslation,
            },
            {
                icon: isMuted ? <VolumeX size={18} /> : <Volume2 size={18} />,
                label: '朗读',
                value: isMuted ? '静音' : '开启',
                active: !isMuted,
            },
            {
                icon: <Activity size={18} />,
                label: '记录',
                value: `${subtitleCount} 条`,
                active: subtitleCount > 0,
            },
        ],
        [hasSpeech, hasTranslation, isMuted, status, subtitleCount]
    );

    useEffect(() => {
        statusRef.current = status;
        volumeRef.current = volume;
        frequencyDataRef.current = frequencyData;
    }, [frequencyData, status, volume]);

    useEffect(() => {
        const canvas = canvasRef.current;
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        const resizeCanvas = () => {
            const dpr = window.devicePixelRatio || 1;
            const rect = canvas.getBoundingClientRect();
            canvas.width = Math.max(1, Math.floor(rect.width * dpr));
            canvas.height = Math.max(1, Math.floor(rect.height * dpr));
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        };

        resizeCanvas();

        const draw = () => {
            const rect = canvas.getBoundingClientRect();
            const width = rect.width;
            const height = rect.height;
            const centerY = height / 2;

            ctx.clearRect(0, 0, width, height);

            const gradient = ctx.createLinearGradient(0, 0, width, 0);
            gradient.addColorStop(0, '#22d3ee');
            gradient.addColorStop(0.5, '#818cf8');
            gradient.addColorStop(1, '#f472b6');

            const currentStatus = statusRef.current;
            const currentVolume = volumeRef.current;
            const bins = frequencyDataRef.current && frequencyDataRef.current.length > 0 ? frequencyDataRef.current : null;
            const barCount = Math.min(96, Math.max(32, Math.floor(width / 9)));
            const gap = 3;
            const barWidth = Math.max(3, (width - gap * (barCount - 1)) / barCount);

            for (let i = 0; i < barCount; i++) {
                const ratio = i / Math.max(1, barCount - 1);
                const sourceIndex = bins ? Math.floor(ratio * (bins.length - 1)) : 0;
                const rawValue = bins ? bins[sourceIndex] / 255 : 0;
                const idleWave = currentStatus === 'recording' ? Math.sin(Date.now() / 280 + i * 0.35) * 0.08 + 0.12 : 0.05;
                const amplitude = Math.max(rawValue, idleWave) * (0.75 + currentVolume * 0.55);
                const barHeight = Math.max(4, amplitude * height * 0.82);
                const x = i * (barWidth + gap);
                const y = centerY - barHeight / 2;

                ctx.fillStyle = gradient;
                ctx.globalAlpha = currentStatus === 'recording' ? 0.95 : 0.35;
                ctx.beginPath();
                ctx.roundRect(x, y, barWidth, barHeight, 4);
                ctx.fill();
            }

            ctx.globalAlpha = 1;
            animationRef.current = requestAnimationFrame(draw);
        };

        window.addEventListener('resize', resizeCanvas);
        draw();

        return () => {
            window.removeEventListener('resize', resizeCanvas);
            cancelAnimationFrame(animationRef.current);
        };
    }, []);

    return (
        <section className={`visual-stage visual-stage-${status}`}>
            <div className="visual-stage-header">
                <div>
                    <span className="visual-stage-kicker">中文 ↔ English</span>
                    <h2>实时同声传译</h2>
                </div>
                <div className="visual-stage-status">
                    <span className="visual-stage-status-dot" />
                    <span>{statusText[status]}</span>
                </div>
            </div>

            <div className="visual-stage-body">
                <div className="visual-text-panel">
                    <span className="visual-text-label">原文</span>
                    <p className={hasSpeech ? '' : 'visual-text-muted'}>
                        {hasSpeech ? currentSourceText : '等待语音输入'}
                    </p>
                </div>

                <div className="visual-wave-panel">
                    <div className="visual-volume-ring" style={{ '--volume': volume } as CSSProperties}>
                        <canvas ref={canvasRef} className="visual-wave-canvas" />
                        <div className="visual-volume-number">
                            <strong>{volumePercent}</strong>
                            <span>%</span>
                        </div>
                    </div>
                </div>

                <div className="visual-text-panel visual-text-panel-target">
                    <span className="visual-text-label">译文</span>
                    <p className={hasTranslation ? '' : 'visual-text-muted'}>
                        {hasTranslation ? currentTargetText : '等待翻译输出'}
                    </p>
                </div>
            </div>

            <div className="visual-stage-metrics">
                {stageItems.map((item) => (
                    <motion.div
                        key={item.label}
                        className={`visual-metric ${item.active ? 'active' : ''}`}
                        initial={{ opacity: 0, y: 8 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ duration: 0.2 }}
                    >
                        <span className="visual-metric-icon">{item.icon}</span>
                        <span className="visual-metric-label">{item.label}</span>
                        <strong>{item.value}</strong>
                    </motion.div>
                ))}
            </div>

            {errorMessage && (
                <div className="visual-error-banner">
                    {errorMessage}
                </div>
            )}
        </section>
    );
}
