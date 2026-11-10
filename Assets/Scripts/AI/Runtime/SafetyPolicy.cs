using System;
namespace RuntimeCreation {
    // Conservative lexical screen, not a general-purpose safety classifier.
    public static class SafetyPolicy {
        static readonly string[] blocked = { "weapon", "firearm", "explosive", "bomb", "poison", "malware", "ransomware", "credential theft", "steal credentials", "kill people", "torture", "sexual content involving minors" };
        public static bool Blocks(string text) { if (string.IsNullOrEmpty(text)) return false; foreach(var term in blocked) if (text.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0) return true; return false; }
    }
}
