using System;
using System.Collections.Generic;
using System.Text;
namespace RuntimeCreation {
    // Extensions are compiled by the developer, never supplied as code by the model.
    public static class EventRegistry {
        sealed class Entry {
            public string description;
            public Action<Binding,Dictionary<string,Item>,Dictionary<string,float>> validate;
            public Func<CreationRuntime,CreationSession,Binding,Action,IDisposable> attach;
        }
        static readonly Dictionary<string,Entry> entries = new Dictionary<string,Entry>();
        static EventRegistry() {
            Builtin("interact","E raycast against a solid object"); Builtin("key","allowlisted gameplay key");
            Builtin("timer","repeating timer"); Builtin("trigger","player enters trigger");
            Builtin("click","UI button click"); Builtin("change","numeric variable changes");
            Builtin("collision","dynamic rigidbody collision");
        }
        static void Builtin(string name,string description) { entries.Add(name,new Entry { description=description }); }
        public static void Register(string name,string description,Action<Binding,Dictionary<string,Item>,Dictionary<string,float>> validate,Func<CreationRuntime,CreationSession,Binding,Action,IDisposable> attach) {
            if(string.IsNullOrWhiteSpace(name) || name.Length>32 || string.IsNullOrWhiteSpace(description) || validate==null || attach==null || entries.ContainsKey(name)) throw new ArgumentException("Invalid or duplicate event registration.");
            foreach(char c in name) if(!(c>='a' && c<='z') && !(c>='0' && c<='9') && c!='_') throw new ArgumentException("Invalid event name.");
            entries.Add(name,new Entry { description=description,validate=validate,attach=attach });
        }
        public static bool Contains(string name) { return name!=null && entries.ContainsKey(name); }
        public static void Validate(Binding binding,Dictionary<string,Item> items,Dictionary<string,float> variables) { var e=entries[binding.on]; if(e.validate!=null) e.validate(binding,items,variables); }
        public static IDisposable Attach(CreationRuntime runtime,CreationSession session,Binding binding,Action emit) { var e=entries[binding.on]; return e.attach==null ? null : e.attach(runtime,session,binding,emit); }
        public static string Describe() { var b=new StringBuilder(); foreach(var e in entries) b.Append(e.Key).Append(": ").Append(e.Value.description).Append("; "); return b.ToString(); }
    }
}
