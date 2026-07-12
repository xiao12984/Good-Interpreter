import { useState, useCallback } from 'react';
import {
  BackgroundEffects,
  Header,
  SubtitleDisplay,
  Controls,
} from './components';
import { useWebSocket, useAudioRecorder } from './hooks';
import type { ConnectionStatus } from './types';
import './App.css';

function App() {
  // Fixed language pair: Chinese ↔ English
  const [sourceLanguage] = useState('zh');
  const [targetLanguage] = useState('en');
  const [volume, setVolume] = useState(0);
  const [frequencyData, setFrequencyData] = useState<Uint8Array | null>(null);

  const {
    status: wsStatus,
    currentAsr,
    currentTranslation,
    subtitles,
    isMuted,
    connect,
    sendAudio,
    sendStop,
    clearSubtitles,
    toggleMute,
  } = useWebSocket({ sourceLanguage, targetLanguage });

  const handleVolumeChange = useCallback(
    (vol: number, freqData: Uint8Array) => {
      setVolume(vol);
      setFrequencyData(freqData);
    },
    []
  );

  const {
    isRecording,
    audioInputMode,
    microphones,
    selectedMicrophoneId,
    startRecording,
    stopRecording,
    selectAudioInputMode,
    selectMicrophone,
    refreshMicrophones,
  } = useAudioRecorder({
    onAudioData: sendAudio,
    onVolumeChange: handleVolumeChange,
  });

  // Update app status based on WebSocket and recording state
  const displayStatus: ConnectionStatus = wsStatus === 'error'
    ? 'error'
    : isRecording
      ? 'recording'
      : wsStatus;

  const handleStart = useCallback(async () => {
    try {
      if (audioInputMode === 'system') {
        await startRecording();
        await connect(audioInputMode);
      } else {
        await connect(audioInputMode);
        await startRecording();
      }
    } catch (error) {
      console.error('Failed to start:', error);
      sendStop();
      stopRecording();
      setVolume(0);
      setFrequencyData(null);

      const message = error instanceof Error
        ? error.message
        : '启动音频输入失败，请检查浏览器权限。';
      window.alert(message);
    }
  }, [audioInputMode, connect, sendStop, startRecording, stopRecording]);

  const handleStop = useCallback(() => {
    stopRecording();
    sendStop();
    setVolume(0);
    setFrequencyData(null);
  }, [stopRecording, sendStop]);

  const handleClear = useCallback(() => {
    clearSubtitles();
  }, [clearSubtitles]);

  return (
    <>
      <BackgroundEffects />

      <div className="app-container">
        <Header status={displayStatus} />

        <main className="main-content">
          <SubtitleDisplay
            subtitles={subtitles}
            isEmpty={subtitles.length === 0 && !currentAsr.text && !currentTranslation.text}
            currentSourceText={isRecording ? currentAsr.text : ''}
            currentTargetText={isRecording ? currentTranslation.text : ''}
          />
        </main>

        <Controls
          isRecording={isRecording}
          audioInputMode={audioInputMode}
          microphones={microphones}
          selectedMicrophoneId={selectedMicrophoneId}
          volume={volume}
          frequencyData={frequencyData}
          isMuted={isMuted}
          onStart={handleStart}
          onStop={handleStop}
          onClear={handleClear}
          onAudioInputModeChange={selectAudioInputMode}
          onMicrophoneChange={selectMicrophone}
          onRefreshMicrophones={refreshMicrophones}
          onToggleMute={toggleMute}
        />
      </div>
    </>
  );
}

export default App;
