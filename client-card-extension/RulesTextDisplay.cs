using System.Text.RegularExpressions;

namespace ArcaneForge {
    public static class RulesTextDisplay {
        // Borrowed abilities can fall back to Arena's generic text entry when
        // the recipient has no printed variant. Those entries contain literal
        // self-reference placeholders; their meaning remains the current card.
        public static string Fix(string text) => text==null?null:
            Regex.Replace(text,@"\b(?:CARDNAME|NICKNAME)\b","this card");
    }
}
