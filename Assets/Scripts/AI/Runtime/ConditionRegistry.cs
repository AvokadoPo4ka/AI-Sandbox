using System;
using System.Collections.Generic;
using System.Text;
namespace RuntimeCreation {
    // Trusted extensions register pure numeric predicates before plans are parsed.
    public static class ConditionRegistry {
        sealed class Entry { public string description; public Func<float,float,bool> predicate; }
        static readonly Dictionary<string,Entry> entries = new Dictionary<string,Entry>();
        static ConditionRegistry() {
            Register("eq","equal",(a,b)=>a==b); Register("ne","not equal",(a,b)=>a!=b);
            Register("lt","less than",(a,b)=>a<b); Register("le","less than or equal",(a,b)=>a<=b);
            Register("gt","greater than",(a,b)=>a>b); Register("ge","greater than or equal",(a,b)=>a>=b);
            Register("near","within 0.001",(a,b)=>Math.Abs(a-b)<=0.001f);
        }
        public static void Register(string name,string description,Func<float,float,bool> predicate) {
            if(string.IsNullOrWhiteSpace(name) || name.Length>32 || string.IsNullOrWhiteSpace(description) || predicate==null || entries.ContainsKey(name)) throw new ArgumentException("Invalid or duplicate condition registration.");
            foreach(char c in name) if(!(c>='a' && c<='z') && !(c>='0' && c<='9') && c!='_') throw new ArgumentException("Invalid condition name.");
            entries.Add(name,new Entry { description=description,predicate=predicate });
        }
        public static bool Contains(string name) { return name!=null && entries.ContainsKey(name); }
        public static bool Evaluate(string name,float actual,float expected) { return entries[name].predicate(actual,expected); }
        public static string Describe() { var b=new StringBuilder(); foreach(var e in entries) b.Append(e.Key).Append(": ").Append(e.Value.description).Append("; "); return b.ToString(); }
    }
}
