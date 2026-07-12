import type { SubtitleItem } from '../types';

const MERGE_GAP_MS = 2500;
const MAX_MERGED_TEXT_LENGTH = 220;
const CJK_PATTERN = /[\u4e00-\u9fff]/;
const HARD_SENTENCE_END_PATTERN = /([。！？!?…]|[.])["'”’)\]]*$/;
const NO_SPACE_BEFORE_PATTERN = /^[,.;:!?，。！？、；：]/;
const CJK_PUNCTUATION_END_PATTERN = /[，。！？、；：]$/;

function hasHardSentenceEnding(text: string) {
    return HARD_SENTENCE_END_PATTERN.test(text.trim());
}

function shouldInsertSpace(left: string, right: string) {
    const leftText = left.trim();
    const rightText = right.trim();
    const leftEnd = leftText.slice(-1);
    const rightStart = rightText.charAt(0);

    if (!leftEnd || !rightStart) return false;
    if (CJK_PATTERN.test(leftEnd) || CJK_PATTERN.test(rightStart)) return false;
    if (CJK_PUNCTUATION_END_PATTERN.test(leftEnd)) return false;
    if (NO_SPACE_BEFORE_PATTERN.test(rightStart)) return false;

    return true;
}

function joinSubtitleText(left: string, right: string) {
    const leftText = left.trim();
    const rightText = right.trim();

    if (!leftText) return rightText;
    if (!rightText) return leftText;

    return shouldInsertSpace(leftText, rightText)
        ? `${leftText} ${rightText}`
        : `${leftText}${rightText}`;
}

function canMergeSubtitle(previous: SubtitleItem, current: SubtitleItem) {
    const sameDirection =
        previous.sourceLanguage === current.sourceLanguage &&
        previous.targetLanguage === current.targetLanguage;

    if (!sameDirection) return false;

    const gapMs = current.timestamp.getTime() - previous.timestamp.getTime();
    if (gapMs < 0 || gapMs > MERGE_GAP_MS) return false;

    if (hasHardSentenceEnding(previous.sourceText)) return false;
    if (hasHardSentenceEnding(previous.targetText)) return false;

    const mergedSourceLength = previous.sourceText.length + current.sourceText.length;
    const mergedTargetLength = previous.targetText.length + current.targetText.length;

    return Math.max(mergedSourceLength, mergedTargetLength) <= MAX_MERGED_TEXT_LENGTH;
}

function mergeSubtitle(previous: SubtitleItem, current: SubtitleItem): SubtitleItem {
    return {
        ...previous,
        sourceText: joinSubtitleText(previous.sourceText, current.sourceText),
        targetText: joinSubtitleText(previous.targetText, current.targetText),
    };
}

export function appendMergedSubtitle(items: SubtitleItem[], item: SubtitleItem): SubtitleItem[] {
    const previous = items.at(-1);

    if (!previous || !canMergeSubtitle(previous, item)) {
        return [...items, item];
    }

    return [...items.slice(0, -1), mergeSubtitle(previous, item)];
}

export function mergeSubtitleItems(items: SubtitleItem[]): SubtitleItem[] {
    return items.reduce<SubtitleItem[]>(
        (mergedItems, item) => appendMergedSubtitle(mergedItems, item),
        []
    );
}
