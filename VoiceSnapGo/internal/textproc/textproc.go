package textproc

import (
	"regexp"
	"strings"
	"unicode"
)

// tagRE matches SenseVoice special tokens like <|zh|>, <|NEUTRAL|>, <|Speech|>.
var tagRE = regexp.MustCompile(`<\|[^|]*\|>`)

// English fillers: whole-word match, case-insensitive
var enFillerRE = regexp.MustCompile(`(?i)\b(?:um|uh|hmm|hm|eh|ah|er|erm|uhm)\b`)

// Chinese multi-char fillers: always safe to remove
var cnFillerMultiRE = regexp.MustCompile(`(?:嗯嗯|啊啊|呃呃|哦哦|嗯啊|嗯呢)`)

// Chinese single-char fillers at start of text (followed by optional punctuation)
var cnFillerStartRE = regexp.MustCompile(`^[嗯呃额哦唔][，,、\s]*`)

// Chinese single-char fillers between punctuation: ，嗯，→ ，
var cnFillerMidRE = regexp.MustCompile(`([，。！？、,!?\s])[嗯呃额哦唔]([，。！？、,!?\s])`)

// isCJK returns true for CJK ideographs (Chinese, Japanese kanji, Korean hanja)
// but NOT CJK punctuation, so we don't insert spaces around punctuation.
func isCJK(r rune) bool {
	return (r >= 0x4E00 && r <= 0x9FFF) || // CJK Unified Ideographs
		(r >= 0x3400 && r <= 0x4DBF) || // CJK Extension A
		(r >= 0x3040 && r <= 0x309F) || // Hiragana
		(r >= 0x30A0 && r <= 0x30FF) || // Katakana
		(r >= 0xF900 && r <= 0xFAFF) || // CJK Compatibility Ideographs
		(r >= 0x20000 && r <= 0x2A6DF) // CJK Extension B
}

func isASCIIAlNum(r rune) bool {
	return (r >= 'a' && r <= 'z') || (r >= 'A' && r <= 'Z') || (r >= '0' && r <= '9')
}

// removeFillers strips common speech filler words (语气词) from text.
func removeFillers(text string) string {
	// English fillers
	text = enFillerRE.ReplaceAllString(text, "")

	// Chinese multi-char fillers (always safe)
	text = cnFillerMultiRE.ReplaceAllString(text, "")

	// Chinese single-char at start
	text = cnFillerStartRE.ReplaceAllString(text, "")

	// Chinese single-char between punctuation (may need multiple passes)
	for i := 0; i < 3; i++ {
		newText := cnFillerMidRE.ReplaceAllString(text, "$1")
		if newText == text {
			break
		}
		text = newText
	}

	return strings.TrimSpace(text)
}

// PostProcess cleans up mixed Chinese-English text from SenseVoice:
//   - Strips special tokens (<|zh|>, <|NEUTRAL|>, etc.)
//   - Inserts spaces between CJK characters and ASCII letters/digits
//   - Capitalizes the first letter and letters after sentence-ending punctuation
//   - Collapses multiple spaces
func PostProcess(text string) string {
	// Strip SenseVoice special tokens
	text = tagRE.ReplaceAllString(text, "")
	text = strings.TrimSpace(text)
	if text == "" {
		return text
	}

	// Remove filler words
	text = removeFillers(text)
	if text == "" {
		return text
	}

	// Insert spaces at CJK ↔ ASCII alphanumeric boundaries
	runes := []rune(text)
	buf := make([]rune, 0, len(runes)+16)
	for i, r := range runes {
		if i > 0 {
			prev := runes[i-1]
			if (isCJK(prev) && isASCIIAlNum(r)) || (isASCIIAlNum(prev) && isCJK(r)) {
				buf = append(buf, ' ')
			}
		}
		buf = append(buf, r)
	}

	// Capitalize first letter and after sentence-ending punctuation
	capNext := true
	for i, r := range buf {
		if capNext && unicode.IsLetter(r) {
			buf[i] = unicode.ToUpper(r)
			capNext = false
		}
		if r == '。' || r == '！' || r == '？' || r == '.' || r == '!' || r == '?' {
			capNext = true
		}
	}

	// Collapse multiple consecutive spaces
	result := string(buf)
	for strings.Contains(result, "  ") {
		result = strings.ReplaceAll(result, "  ", " ")
	}

	return strings.TrimSpace(result)
}
