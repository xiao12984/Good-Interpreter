"""
Shared language direction helpers for bidirectional translation.

Keeping these rules outside the WebSocket handler makes the UI direction lock
easy to review and test without opening real AST or browser connections.
"""

import re


ZH_TO_EN = "zh→en"
EN_TO_ZH = "en→zh"

_CHINESE_PATTERN = re.compile(r"[\u4e00-\u9fff]")
_PUNCTUATION_ONLY_PATTERN = re.compile(r"[，。！？,.!?\s\"'“”‘’]+")


def has_chinese(text: str) -> bool:
    """Return True when text contains Chinese CJK characters."""
    return bool(_CHINESE_PATTERN.search(text))


def is_meaningful_text(text: str) -> bool:
    """Return False for empty text or punctuation-only ASR fragments."""
    if not text:
        return False

    return len(_PUNCTUATION_ONLY_PATTERN.sub("", text)) > 0


def get_expected_direction(text: str) -> str:
    """Infer the translation direction expected by the ASR text language."""
    return ZH_TO_EN if has_chinese(text) else EN_TO_ZH


def get_effective_asr_direction(direction: str, text: str) -> str:
    """Use the AST direction when present, otherwise infer it from ASR text."""
    return direction or get_expected_direction(text)


def is_asr_direction_valid(direction: str, text: str) -> bool:
    """Reject ASR emitted by the opposite AST session."""
    return not direction or direction == get_expected_direction(text)


def should_accept_translation(direction: str, active_direction: str) -> bool:
    """Accept translation only after ASR has locked the current sentence."""
    return bool(active_direction) and direction == active_direction


def get_language_pair(direction: str) -> tuple[str, str]:
    """Return source and target language codes for a translation direction."""
    if direction == EN_TO_ZH:
        return "en", "zh"

    return "zh", "en"
