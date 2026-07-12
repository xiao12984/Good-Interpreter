"""Unit tests for bidirectional language direction rules."""

import sys
import unittest
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.services.language_direction import (  # noqa: E402
    EN_TO_ZH,
    ZH_TO_EN,
    get_expected_direction,
    get_language_pair,
    is_asr_direction_valid,
    is_meaningful_text,
    should_accept_translation,
)


class LanguageDirectionTests(unittest.TestCase):
    """Protect the UI-facing bilingual direction lock rules."""

    def test_expected_direction_follows_asr_language(self):
        self.assertEqual(get_expected_direction("你好"), ZH_TO_EN)
        self.assertEqual(get_expected_direction("hello"), EN_TO_ZH)

    def test_punctuation_only_text_is_not_meaningful(self):
        self.assertFalse(is_meaningful_text("，。！？ "))
        self.assertTrue(is_meaningful_text("hello!"))

    def test_asr_from_wrong_ast_session_is_rejected(self):
        self.assertFalse(is_asr_direction_valid(ZH_TO_EN, "hello"))
        self.assertFalse(is_asr_direction_valid(EN_TO_ZH, "你好"))
        self.assertTrue(is_asr_direction_valid("", "你好"))

    def test_translation_requires_locked_active_direction(self):
        self.assertTrue(should_accept_translation(ZH_TO_EN, ZH_TO_EN))
        self.assertFalse(should_accept_translation(EN_TO_ZH, ZH_TO_EN))
        self.assertFalse(should_accept_translation(ZH_TO_EN, ""))

    def test_language_pair_matches_direction(self):
        self.assertEqual(get_language_pair(ZH_TO_EN), ("zh", "en"))
        self.assertEqual(get_language_pair(EN_TO_ZH), ("en", "zh"))


if __name__ == "__main__":
    unittest.main()
