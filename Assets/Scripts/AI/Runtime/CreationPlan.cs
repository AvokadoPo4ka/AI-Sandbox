using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCreation {
    public sealed class Item {
        public string id, kind, parent, anchor, collider, body, text, color;
        public Vector3 position, rotation, scale;
    }
    public sealed class ActionSpec {
        public string type, target, text, color, variable;
        public Dictionary<string,object> parameters = new Dictionary<string,object>();
        public Vector3 offset, rotation;
        public float duration, amount, value, delay;
    }
    public sealed class ConditionSpec { public string variable, op; public float value; }
    public sealed class Binding {
        public string on, target, key;
        public float seconds;
        public List<ActionSpec> actions = new List<ActionSpec>();
        public List<ConditionSpec> conditions = new List<ConditionSpec>();
    }
    public sealed class JointSpec { public string source, target, kind; public Vector3 anchor, axis; public bool limited; public float min, max; }
    public sealed class Plan {
        public string message;
        public List<Item> items = new List<Item>();
        public Dictionary<string,float> variables = new Dictionary<string,float>();
        public List<Binding> bindings = new List<Binding>();
        public List<JointSpec> joints = new List<JointSpec>();
    }

    // This parser is a separate trust boundary: unrecognized fields are errors, not ignored.
    public static class PlanReader {
        static Dictionary<string,object> Obj(object o, string where) { var x = o as Dictionary<string,object>; if (x == null) throw new FormatException(where + " must be an object."); return x; }
        static List<object> Arr(object o, string where) { var x = o as List<object>; if (x == null) throw new FormatException(where + " must be an array."); return x; }
        static void Fields(Dictionary<string,object> d, params string[] allowed) { var set = new HashSet<string>(allowed); foreach (var k in d.Keys) if (!set.Contains(k)) throw new FormatException("Unknown field: " + k); }
        static string Str(Dictionary<string,object> d, string key, string fallback = null, int max = 200) { object o; if (!d.TryGetValue(key, out o)) { if (fallback == null) throw new FormatException("Missing field: " + key); return fallback; } var s = o as string; if (s == null || s.Length > max) throw new FormatException("Invalid string: " + key); return s; }
        static float Num(Dictionary<string,object> d, string key, float fallback, float min, float max) { object o; if (!d.TryGetValue(key, out o)) return fallback; if (!(o is double)) throw new FormatException(key + " must be a number."); double n = (double)o; if (n < min || n > max) throw new FormatException(key + " out of range."); return (float)n; }
        static Vector3 Vec(Dictionary<string,object> d, string key, Vector3 fallback, float min, float max) { object o; if (!d.TryGetValue(key,out o)) return fallback; var a = Arr(o,key); if (a.Count != 3) throw new FormatException(key + " must contain three numbers."); float[] v = new float[3]; for (int i=0;i<3;i++) { if (!(a[i] is double) || (double)a[i] < min || (double)a[i] > max) throw new FormatException(key + " component out of range."); v[i]=(float)(double)a[i]; } return new Vector3(v[0],v[1],v[2]); }
        static void OneOf(string x, string label, params string[] choices) { if (Array.IndexOf(choices,x)<0) throw new FormatException("Unknown " + label + ": " + x); }
        static void Id(string id) { if (id.Length < 1 || id.Length > 32) throw new FormatException("IDs must be 1–32 characters."); foreach(char c in id) if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_' && c != '-') throw new FormatException("Invalid ID: " + id); }
        static void ColorValue(string c) { Color unused; if (c.Length != 7 || c[0]!='#' || !ColorUtility.TryParseHtmlString(c,out unused)) throw new FormatException("Color must be #RRGGBB."); }
        public static Plan Read(string json, CreationBudgets limits = null) {
            limits=limits ?? new CreationBudgets(); limits.Validate();
            var root = Obj(StrictJson.Parse(json),"plan"); Fields(root,"version","message","objects","bindings","variables","joints");
            if (Num(root,"version",-1,1,1)!=1) throw new FormatException("Unsupported or missing schema version.");
            var plan = new Plan(); plan.message = Str(root,"message","Creation complete.",240);
            object rawVariables;
            if (root.TryGetValue("variables",out rawVariables)) foreach(object entry in Arr(rawVariables,"variables")) {
                var d=Obj(entry,"variable"); Fields(d,"id","initial"); string name=Str(d,"id"); Id(name);
                if (plan.variables.Count>=limits.actions || plan.variables.ContainsKey(name)) throw new FormatException("Duplicate variable or variable budget exceeded: " + name);
                plan.variables.Add(name,Num(d,"initial",0,-1000000,1000000));
            }
            object raw; if (!root.TryGetValue("objects",out raw)) throw new FormatException("Missing objects.");
            var objects = Arr(raw,"objects"); if (objects.Count > limits.objects) throw new FormatException("Object budget exceeded.");
            var byId = new Dictionary<string,Item>();
            foreach (object entry in objects) {
                var d = Obj(entry,"object"); Fields(d,"id","kind","parent","anchor","position","rotation","scale","color","collider","body","text");
                var x = new Item(); x.id=Str(d,"id"); Id(x.id); x.kind=Str(d,"kind"); if (!ObjectBuilderRegistry.Contains(x.kind) && x.kind!="light" && x.kind!="label" && x.kind!="button") throw new FormatException("Unknown object kind: " + x.kind);
                x.parent=Str(d,"parent",""); x.anchor=Str(d,"anchor","front"); OneOf(x.anchor,"anchor","front","world","camera");
                x.position=Vec(d,"position",Vector3.zero,-100,100); x.rotation=Vec(d,"rotation",Vector3.zero,-360,360); x.scale=Vec(d,"scale",Vector3.one,0.05f,30);
                x.color=Str(d,"color","#FFFFFF"); ColorValue(x.color); x.collider=Str(d,"collider","none"); OneOf(x.collider,"collider","none","solid","trigger");
                x.body=Str(d,"body","none"); OneOf(x.body,"body","none","kinematic","dynamic"); x.text=Str(d,"text","",160); if (SafetyPolicy.Blocks(x.text)) throw new FormatException("Object text rejected by safety filter.");
                if ((x.kind=="label" || x.kind=="button" || x.kind=="light") && (x.collider!="none" || x.body!="none")) throw new FormatException(x.id + ": UI/light cannot have physics.");
                if (x.body!="none" && x.collider=="none") throw new FormatException(x.id + ": rigidbody needs a collider.");
                if (x.body=="dynamic" && (x.kind=="plane" || x.kind=="quad")) throw new FormatException(x.id + ": dynamic plane/quad is not supported.");
                if (byId.ContainsKey(x.id)) throw new FormatException("Duplicate object ID: " + x.id); byId.Add(x.id,x); plan.items.Add(x);
            }
            foreach (var x in plan.items) {
                var seen = new HashSet<string>(); var cur=x; int depth=0;
                while (cur.parent!="") { if (!seen.Add(cur.id) || ++depth>limits.depth) throw new FormatException("Cyclic or too-deep hierarchy: " + x.id); Item parent; if (!byId.TryGetValue(cur.parent,out parent)) throw new FormatException("Unknown parent: " + cur.parent); if (parent.kind=="label" || parent.kind=="button" || cur.kind=="label" || cur.kind=="button") throw new FormatException("UI items cannot have parents."); cur=parent; }
                if (cur.anchor=="camera" && (x.collider!="none" || x.body!="none")) throw new FormatException("Camera attachments cannot have colliders or rigidbodies.");
                if ((x.kind=="label" || x.kind=="button") && x.parent!="") throw new FormatException("UI must be at root.");
            }
            object rawJoints;
            if(root.TryGetValue("joints",out rawJoints)) {
                var joints=Arr(rawJoints,"joints"); if(joints.Count>limits.objects) throw new FormatException("Joint budget exceeded.");
                foreach(object entry in joints) {
                    var d=Obj(entry,"joint"); Fields(d,"source","target","kind","anchor","axis","limited","min","max");
                    var j=new JointSpec { source=Str(d,"source"), target=Str(d,"target"), kind=Str(d,"kind"), anchor=Vec(d,"anchor",Vector3.zero,-10,10), axis=Vec(d,"axis",new Vector3(0,1,0),-1,1), min=Num(d,"min",-90,-180,180), max=Num(d,"max",90,-180,180) };
                    object limited; if(d.TryGetValue("limited",out limited)) { if(!(limited is bool)) throw new FormatException("limited must be boolean."); j.limited=(bool)limited; }
                    OneOf(j.kind,"joint kind","fixed","hinge"); Item a,b;
                    if(j.source==j.target || !byId.TryGetValue(j.source,out a) || !byId.TryGetValue(j.target,out b)) throw new FormatException("Joint needs two distinct existing objects.");
                    if(a.body!="dynamic" || b.body=="none" || a.collider!="solid" || b.collider!="solid") throw new FormatException("Joint source must be dynamic, target needs a rigidbody, both need solid colliders.");
                    if(j.axis.x*j.axis.x+j.axis.y*j.axis.y+j.axis.z*j.axis.z<0.0001f || j.min>j.max) throw new FormatException("Invalid joint axis or limits.");
                    plan.joints.Add(j);
                }
            }
            if (!root.TryGetValue("bindings",out raw)) throw new FormatException("Missing bindings array."); var bindings=Arr(raw,"bindings"); if (bindings.Count>limits.bindings) throw new FormatException("Binding budget exceeded."); if (objects.Count==0 && bindings.Count!=0) throw new FormatException("Empty plans cannot have bindings."); int actionCount=0; var usedKeys=new HashSet<string>();
            foreach (object entry in bindings) {
                var d=Obj(entry,"binding"); Fields(d,"on","target","key","seconds","actions","conditions"); var b=new Binding(); b.on=Str(d,"on"); if (!EventRegistry.Contains(b.on)) throw new FormatException("Unknown event: " + b.on); b.target=Str(d,"target",""); b.key=Str(d,"key",""); b.seconds=Num(d,"seconds",1,0.25f,3600);
                Item source;
                if (b.on=="interact" || b.on=="trigger" || b.on=="click" || b.on=="collision") { if (!byId.TryGetValue(b.target,out source)) throw new FormatException("Unknown event target: " + b.target); if (b.on=="interact" && source.collider!="solid") throw new FormatException("Interaction target needs a solid collider."); if (b.on=="trigger" && source.collider!="trigger") throw new FormatException("Trigger target needs a trigger collider."); if (b.on=="collision" && (source.collider!="solid" || source.body!="dynamic")) throw new FormatException("Collision target needs a solid collider and dynamic rigidbody."); if (b.on=="click" && source.kind!="button") throw new FormatException("Click target must be a button."); }
                if (b.on=="change" && !plan.variables.ContainsKey(b.target)) throw new FormatException("Unknown change variable: " + b.target);
                object conditionRaw;
                if (d.TryGetValue("conditions",out conditionRaw)) { var list=Arr(conditionRaw,"conditions"); if (list.Count>16) throw new FormatException("Too many conditions."); foreach(object c in list) { var cd=Obj(c,"condition"); Fields(cd,"variable","op","value"); var condition=new ConditionSpec { variable=Str(cd,"variable"), op=Str(cd,"op"), value=Num(cd,"value",0,-1000000,1000000) }; if (!plan.variables.ContainsKey(condition.variable)) throw new FormatException("Unknown condition variable: " + condition.variable); if (!ConditionRegistry.Contains(condition.op)) throw new FormatException("Unknown comparison: " + condition.op); b.conditions.Add(condition); } }
                if (b.on=="key") { OneOf(b.key,"key","F","G","H","J","K","L"); if (!usedKeys.Add(b.key)) throw new FormatException("Duplicate key binding: " + b.key); }
                if (!d.TryGetValue("actions",out raw)) throw new FormatException("Binding needs actions."); var actions=Arr(raw,"actions"); if (actions.Count<1 || actions.Count>limits.actions) throw new FormatException("Invalid number of actions in binding.");
                foreach (object a in actions) { if (++actionCount>limits.actions) throw new FormatException("Action budget exceeded."); var ad=Obj(a,"action"); var spec=ActionRegistry.Parse(ad,byId,plan.variables); b.actions.Add(spec); }
                EventRegistry.Validate(b,byId,plan.variables);
                plan.bindings.Add(b);
            }
            return plan;
        }
        // Shared validation helpers for trusted action registrations.
        public static ActionSpec ParseAction(Dictionary<string,object> d, Dictionary<string,Item> items, Dictionary<string,float> variables) {
            Fields(d,"type","target","offset","duration","amount","text","color","value","variable","delay","rotation","params"); var a=new ActionSpec(); a.type=Str(d,"type"); a.target=Str(d,"target"); Item item=null;
            if (ActionRegistry.TargetKind(a.type)=="variable") { if (!variables.ContainsKey(a.target)) throw new FormatException("Unknown target variable: " + a.target); }
            else if (!items.TryGetValue(a.target,out item)) throw new FormatException("Unknown action target: " + a.target);
            a.offset=Vec(d,"offset",Vector3.zero,-20,20); a.rotation=Vec(d,"rotation",Vector3.zero,-360,360); a.duration=Num(d,"duration",1,0.05f,60); a.amount=Num(d,"amount",1,-1000,1000); a.value=Num(d,"value",0,-1000000,1000000); a.delay=Num(d,"delay",0,0,60); a.variable=Str(d,"variable",""); a.text=Str(d,"text","",160); if (SafetyPolicy.Blocks(a.text)) throw new FormatException("Action text rejected by safety filter."); a.color=Str(d,"color","#FFFFFF"); ColorValue(a.color);
            if (a.variable!="" && !variables.ContainsKey(a.variable)) throw new FormatException("Unknown action variable: " + a.variable);
            object parameters; if (d.TryGetValue("params",out parameters)) a.parameters=Obj(parameters,"params");
            ActionRegistry.Validate(a,item,items,variables);
            return a;
        }
    }
}
