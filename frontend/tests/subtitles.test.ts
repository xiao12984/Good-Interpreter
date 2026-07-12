import assert from 'node:assert/strict';
import test from 'node:test';

import type { SubtitleItem } from '../src/types/index.js';
import { appendMergedSubtitle, canMergeSubtitle } from '../src/utils/subtitles.js';

function subtitle(
    id: string,
    sourceText: string,
    targetText: string,
    sourceLanguage: string,
    targetLanguage: string,
    offsetMs: number
): SubtitleItem {
    return {
        id,
        timestamp: new Date(1_700_000_000_000 + offsetMs),
        sourceText,
        targetText,
        sourceLanguage,
        targetLanguage,
    };
}

test('merge accepts equivalent language codes such as zh and zh-CN', () => {
    const first = subtitle('1', '你好', 'hello', 'zh-CN', 'en', 0);
    const second = subtitle('2', '世界', 'world', 'zh', 'en-US', 500);

    assert.equal(canMergeSubtitle(first, second), true);
});

test('merge rejects cross-direction subtitle items', () => {
    const zhToEn = subtitle('1', '你好', 'hello', 'zh', 'en', 0);
    const enToZh = subtitle('2', 'world', '世界', 'en', 'zh', 500);

    assert.equal(canMergeSubtitle(zhToEn, enToZh), false);
});

test('appendMergedSubtitle keeps hard sentence boundaries separate', () => {
    const first = subtitle('1', 'hello.', '你好。', 'en', 'zh', 0);
    const second = subtitle('2', 'world', '世界', 'en', 'zh', 500);

    assert.equal(appendMergedSubtitle([first], second).length, 2);
});
