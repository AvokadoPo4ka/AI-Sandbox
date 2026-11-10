using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RuntimeCreation {
    // Builders are trusted precompiled code. They must return a fresh primitive-like object
    // with Renderer and Collider; the transaction owns and destroys it on rollback.
    public static class ObjectBuilderRegistry {
        sealed class Entry { public string description; public Func<GameObject> factory; }
        static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        static ObjectBuilderRegistry() {
            Register("ramp","Procedural wedge with a convex mesh collider",RampGeometry.Create);
            Register("cube","Built-in cube",()=>GameObject.CreatePrimitive(PrimitiveType.Cube));
            Register("sphere","Built-in sphere",()=>GameObject.CreatePrimitive(PrimitiveType.Sphere));
            Register("capsule","Built-in capsule",()=>GameObject.CreatePrimitive(PrimitiveType.Capsule));
            Register("cylinder","Built-in cylinder",()=>GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            Register("plane","Built-in plane",()=>GameObject.CreatePrimitive(PrimitiveType.Plane));
            Register("quad","Built-in quad",()=>GameObject.CreatePrimitive(PrimitiveType.Quad));
        }
        public static void Register(string name,string description,Func<GameObject> factory) {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description) || factory==null || entries.ContainsKey(name) || name=="label" || name=="button" || name=="light") throw new ArgumentException("Invalid or duplicate object builder.");
            entries.Add(name,new Entry { description=description, factory=factory });
        }
        public static bool Contains(string name) { return entries.ContainsKey(name); }
        public static GameObject Build(string name) { return entries[name].factory(); }
        public static string Describe() { var b=new StringBuilder(); foreach(var e in entries) b.Append(e.Key).Append(": ").Append(e.Value.description).Append("; "); return b.ToString(); }
    }
}
