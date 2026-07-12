import assert from 'node:assert/strict';
import test from 'node:test';

import { appendPendingAudioChunk } from '../src/utils/pendingAudio.js';

test('microphone overflow keeps streaming WAV header and newest audio chunks', () => {
    const result = ['wav-header', 'old-audio', 'new-audio'].reduce(
        (pendingAudio, chunk) => appendPendingAudioChunk(pendingAudio, chunk, 'microphone', 2),
        [] as string[]
    );

    assert.deepEqual(result, ['wav-header', 'new-audio']);
});

test('system audio overflow keeps newest PCM chunks only', () => {
    const result = ['old-pcm', 'mid-pcm', 'new-pcm'].reduce(
        (pendingAudio, chunk) => appendPendingAudioChunk(pendingAudio, chunk, 'system', 2),
        [] as string[]
    );

    assert.deepEqual(result, ['mid-pcm', 'new-pcm']);
});
