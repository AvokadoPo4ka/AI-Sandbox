using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCreation {
    // Only trusted precompiled registrations can add actions. Model data never names CLR types.
    public sealed class ParameterRule {
        public string name, kind; public bool required; public float min, max;
        public ParameterRule(string name, string kind, bool required=false, float min=-1000000, float max=1000000) { this.name=name; this.kind=kind; this.required=required; this.min=min; this.max=max; }
    }
    public static class ActionRegistry {
        sealed class Entry { public string description, targetKind; public ParameterRule[] parameters; public Action<ActionSpec,Item> validate; public Action<CreationRuntime,CreationSession,ActionSpec,GameObject> execute; }
        static readonly Dictionary<string,Entry> entries = new Dictionary<string,Entry>();
        static ActionRegistry() {
            Register("move", "object", "offset [x,y,z], duration; toggles between initial position and offset", null, (a,x) => NonDynamic3D(x), (r,s,a,g) => r.MoveAction(s,a,g));
            Register("rotate", "object", "rotation [x,y,z] degrees, duration; toggles between initial rotation and offset", null, (a,x) => NonDynamic3D(x), (r,s,a,g) => r.RotateAction(s,a,g));
            Register("toggle", "object", "toggle active state of target", null, (a,x) => {}, (r,s,a,g) => g.SetActive(!g.activeSelf));
            Register("counter", "object", "increment label counter by amount, display optional text prefix", null, (a,x) => RequireLabel(x), (r,s,a,g) => { float n; s.counters.TryGetValue(a.target,out n); n=Mathf.Clamp(n+a.amount,-1000000,1000000); s.counters[a.target]=n; var t=g.GetComponentInChildren<Text>(true); if(t) t.text=a.text+n.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture); });
            Register("text", "object", "set label text", null, (a,x) => RequireLabel(x), (r,s,a,g) => { var t=g.GetComponentInChildren<Text>(true); if(t) t.text=a.text; });
            Register("color", "object", "set primitive color #RRGGBB", null, (a,x) => { if(x.kind=="label" || x.kind=="button" || x.kind=="light") throw new FormatException("Color requires a primitive."); }, (r,s,a,g) => { var v=g.GetComponent<Renderer>(); Color c; if(v && ColorUtility.TryParseHtmlString(a.color,out c)) v.sharedMaterial.color=c; });
            Register("light", "object", "toggle light on/off", null, (a,x) => { if(x.kind!="light") throw new FormatException("Light action requires a light."); }, (r,s,a,g) => { var l=g.GetComponent<Light>(); if(l) l.enabled=!l.enabled; });
            Register("set_var", "variable", "set target variable to value, then fire change event if changed", null, (a,x) => {}, (r,s,a,g) => r.SetVariable(s,a.target,a.value));
            Register("add_var", "variable", "add amount to target variable, then fire change event if changed", null, (a,x) => {}, (r,s,a,g) => r.SetVariable(s,a.target,s.variables[a.target]+a.amount));
            Register("show_var", "object", "display variable value in target label, prefixed with text; variable is required", null, (a,x) => { RequireLabel(x); if(a.variable=="") throw new FormatException("show_var needs variable."); }, (r,s,a,g) => { var t=g.GetComponentInChildren<Text>(true); if(t) t.text=a.text+s.variables[a.variable].ToString("0.##",System.Globalization.CultureInfo.InvariantCulture); });
            Register("freeze", "object", "set Rigidbody constraint mask; params.mask is an even integer 0..126 (position X/Y/Z bits 2/4/8; rotation X/Y/Z bits 16/32/64)", new[]{new ParameterRule("mask","number",true,0,126)}, (a,x) => { if(x.body!="dynamic") throw new FormatException("Constraints require a dynamic rigidbody."); double n=(double)a.parameters["mask"]; if(n!=Math.Floor(n) || ((int)n & ~126)!=0) throw new FormatException("Invalid constraint mask."); }, (r,s,a,g) => { var body=g.GetComponent<Rigidbody>(); if(body) body.constraints=(RigidbodyConstraints)(int)(double)a.parameters["mask"]; });
            Register("impulse", "object", "apply offset as a bounded world-space impulse to a dynamic rigidbody", null, (a,x) => { if(x.body!="dynamic") throw new FormatException("Impulse requires a dynamic rigidbody."); }, (r,s,a,g) => { var b=g.GetComponent<Rigidbody>(); if(b && !b.isKinematic) b.AddForce(a.offset,ForceMode.Impulse); });
        }
        static void NonDynamic3D(Item x) { if (x.kind=="label" || x.kind=="button" || x.body=="dynamic") throw new FormatException("Action requires a non-dynamic 3D object."); }
        static void RequireLabel(Item x) { if(x.kind!="label") throw new FormatException("Action requires a label."); }
        public static void Register(string name,string targetKind,string description,ParameterRule[] parameters,Action<ActionSpec,Item> validate,Action<CreationRuntime,CreationSession,ActionSpec,GameObject> execute) {
            if (string.IsNullOrWhiteSpace(name) || (targetKind!="object" && targetKind!="variable") || string.IsNullOrWhiteSpace(description) || validate==null || execute==null || entries.ContainsKey(name)) throw new ArgumentException("Invalid or duplicate action registration.");
            var rules=parameters ?? new ParameterRule[0]; var names=new HashSet<string>();
            foreach(var rule in rules) if(rule==null || string.IsNullOrWhiteSpace(rule.name) || !names.Add(rule.name) || Array.IndexOf(new[]{"number","bool","string","object_id","variable_id","vector3"},rule.kind)<0 || rule.min>rule.max) throw new ArgumentException("Invalid action parameter rule.");
            entries.Add(name,new Entry { targetKind=targetKind,description=description,parameters=rules,validate=validate,execute=execute });
        }
        public static string TargetKind(string type) { return entries[type].targetKind; }
        public static ActionSpec Parse(Dictionary<string,object> data,Dictionary<string,Item> items,Dictionary<string,float> vars) { object type; if(!data.TryGetValue("type",out type) || !(type is string) || !entries.ContainsKey((string)type)) throw new FormatException("Unknown action type."); return PlanReader.ParseAction(data,items,vars); }
        public static void Validate(ActionSpec a, Item item,Dictionary<string,Item> items,Dictionary<string,float> vars) {
            var e=entries[a.type]; var allowed=new Dictionary<string,ParameterRule>(); foreach(var r in e.parameters) allowed.Add(r.name,r);
            foreach(var kv in a.parameters) {
                ParameterRule rule; if(!allowed.TryGetValue(kv.Key,out rule)) throw new FormatException("Unknown parameter: "+kv.Key);
                var v=kv.Value;
                if(rule.kind=="number" && (!(v is double) || (double)v<rule.min || (double)v>rule.max)) throw new FormatException("Invalid number parameter: "+kv.Key);
                if(rule.kind=="bool" && !(v is bool)) throw new FormatException("Invalid bool parameter: "+kv.Key);
                if(rule.kind=="string" && (!(v is string) || ((string)v).Length>160 || SafetyPolicy.Blocks((string)v))) throw new FormatException("Invalid text parameter: "+kv.Key);
                if(rule.kind=="object_id" && (!(v is string) || !items.ContainsKey((string)v))) throw new FormatException("Invalid object reference: "+kv.Key);
                if(rule.kind=="variable_id" && (!(v is string) || !vars.ContainsKey((string)v))) throw new FormatException("Invalid variable reference: "+kv.Key);
                if(rule.kind=="vector3") { var list=v as List<object>; if(list==null || list.Count!=3) throw new FormatException("Invalid vector: "+kv.Key); foreach(var n in list) if(!(n is double) || (double)n<rule.min || (double)n>rule.max) throw new FormatException("Invalid vector component: "+kv.Key); }
            }
            foreach(var rule in e.parameters) if(rule.required && !a.parameters.ContainsKey(rule.name)) throw new FormatException("Missing parameter: "+rule.name);
            e.validate(a,item);
        }
        public static string Describe() { var b=new System.Text.StringBuilder(); foreach(var pair in entries) { var e=pair.Value; b.Append(pair.Key).Append(" target=").Append(e.targetKind).Append(": ").Append(e.description); foreach(var p in e.parameters) b.Append("; params.").Append(p.name).Append("=").Append(p.kind).Append(p.required ? " (required)" : " (optional)").Append(" range ").Append(p.min).Append("..").Append(p.max); b.Append("\n"); } return b.ToString(); }
        public static void Execute(CreationRuntime r,CreationSession s,ActionSpec a,GameObject go) { entries[a.type].execute(r,s,a,go); }
    }
    public sealed class CreationSession {
        public GameObject frontRoot, worldRoot, cameraRoot, uiRoot;
        public readonly Dictionary<string,GameObject> objects = new Dictionary<string,GameObject>();
        public readonly Dictionary<string,float> counters = new Dictionary<string,float>();
        public readonly Dictionary<string,float> variables = new Dictionary<string,float>();
        public readonly Dictionary<string,Quaternion> rotations = new Dictionary<string,Quaternion>();
        public CreationBudgets budgets; public int pendingActions, depth, dispatchFrame=-1, dispatchCount;
        public readonly Dictionary<string,Vector3> starts = new Dictionary<string,Vector3>();
        public readonly Dictionary<string,bool> moved = new Dictionary<string,bool>();
        public readonly Dictionary<string,Coroutine> movements = new Dictionary<string,Coroutine>();
        public readonly List<Material> materials = new List<Material>();
        public readonly List<GameObject> allocated = new List<GameObject>();
        public readonly List<Binding> bindings = new List<Binding>();
        public readonly List<IDisposable> subscriptions = new List<IDisposable>();
        public bool alive, disposed;
        public readonly List<Coroutine> timers = new List<Coroutine>();
        static void Cleanup(Action action) {
            try { action(); } catch(Exception e) { Debug.LogWarning("AI cleanup could not be completed: " + e.Message); }
        }
        public void Dispose() {
            if (disposed) return;
            disposed=true; alive=false;
            foreach(var subscription in subscriptions) { var current=subscription; Cleanup(() => current.Dispose()); }
            subscriptions.Clear();
            // Destroy is deferred by Unity. Deactivate first and do not claim immediate verified removal.
            foreach(var root in new[]{frontRoot,worldRoot,cameraRoot,uiRoot}) {
                var current=root; if (current) { Cleanup(() => current.SetActive(false)); Cleanup(() => UnityEngine.Object.Destroy(current)); }
            }
            foreach (var obj in allocated) { var current=obj; if (current) { Cleanup(() => current.SetActive(false)); Cleanup(() => UnityEngine.Object.Destroy(current)); } }
            foreach (var material in materials) { var current=material; if (current) Cleanup(() => UnityEngine.Object.Destroy(current)); }
        }
    }
    public sealed class TriggerRelay : MonoBehaviour {
        public CreationRuntime owner; public CreationSession session; public string target;
        void OnTriggerEnter(Collider other) { if (owner != null) owner.Triggered(session,target,other); }
        void OnCollisionEnter(Collision other) { if (owner != null) owner.Collided(session,target); }
    }
    public sealed class CreationRuntime : MonoBehaviour {
        readonly List<CreationSession> sessions = new List<CreationSession>();
        static readonly KeyCode[] Keys = { KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L };
        Transform player; Camera playerCamera; Canvas canvas; Font font; Text hint; float nextHint;
        CreationSession pending; CreationBudgets budgets; Action<string> report; int lastErrorFrame = -1; bool shuttingDown;
        public void Initialize(Transform p, Camera c, Canvas ui, Font f, Text interactionHint, CreationBudgets limits, Action<string> onError) { player=p; playerCamera=c; canvas=ui; font=f; hint=interactionHint; budgets=limits; report=onError; }
        // Stop creation work before a scene reload or owning component shutdown.
        public void CancelPending() {
            var staged=pending; pending=null;
            if (staged!=null) { StopSessionTimers(staged); staged.Dispose(); }
        }
        void StopSessionTimers(CreationSession session) {
            foreach (var timer in session.timers) if (timer!=null) StopCoroutine(timer);
            session.timers.Clear();
        }
        public void ShutdownForSceneRestart() {
            shuttingDown=true;
            StopAllCoroutines();
            CancelPending();
            foreach (var session in sessions) session.Dispose();
            sessions.Clear();
        }
        public void ResumeAfterFailedSceneRestart() { shuttingDown=false; }
        public IEnumerator Build(Plan plan, Action<float> progress, Func<bool> cancelled, Action<string> completed) {
            if (shuttingDown || pending!=null) { completed("Creation is unavailable while shutting down or another build is active."); yield break; }
            var session = new CreationSession(); session.budgets=budgets; foreach(var v in plan.variables) session.variables.Add(v.Key,v.Value); pending=session; string failure = null;
            try {
                if (cancelled()) throw new OperationCanceledException("Cancelled.");
                if (sessions.Count>=budgets.sessions) throw new InvalidOperationException("Creation limit reached; restart the scene to clear creations.");
                foreach (var b in plan.bindings) if (b.on=="key") foreach(var existing in sessions) foreach(var prior in existing.bindings) if (prior.on=="key" && prior.key==b.key) throw new InvalidOperationException("Key " + b.key + " is already used by a creation.");
                if (!player || !playerCamera || !canvas || !font) throw new InvalidOperationException("Player, camera, Canvas or UI font missing.");
                session.frontRoot=Root("AI creation - front",null); session.worldRoot=Root("AI creation - world",null); session.cameraRoot=Root("AI creation - camera",playerCamera.transform); session.uiRoot=Root("AI creation - UI",canvas.transform, true);
                var forward=Vector3.ProjectOnPlane(playerCamera.transform.forward,Vector3.up).normalized; if (forward.sqrMagnitude<0.01f) forward=Vector3.ProjectOnPlane(player.forward,Vector3.up).normalized;
                Vector3 place=player.position + forward*4f; RaycastHit hit; if (Physics.Raycast(place+Vector3.up*10,Vector3.down,out hit,30,Fragsurf.Movement.SurfPhysics.groundLayerMask,QueryTriggerInteraction.Ignore)) place.y=hit.point.y;
                session.frontRoot.transform.position=place; session.frontRoot.transform.rotation=Quaternion.LookRotation(forward,Vector3.up);
            } catch(Exception e) { failure=e.Message; }
            var remaining = new List<Item>(plan.items); int built=0;
            while (remaining.Count>0 && failure==null) {
                if (cancelled() || session.disposed || shuttingDown) { failure="Cancelled."; break; }
                bool found=false;
                for (int i=0;i<remaining.Count;i++) { var item=remaining[i]; if (item.parent!="" && !session.objects.ContainsKey(item.parent)) continue;
                    try { MakeItem(session,item); } catch(Exception e) { failure="Could not create " + item.id + ": " + e.Message; }
                    remaining.RemoveAt(i); found=true; built++; progress((float)built/(plan.items.Count+1)); break;
                }
                if (!found) failure="Unable to resolve object dependencies.";
                yield return null;
            }
            if (failure==null && !cancelled() && !session.disposed && !shuttingDown) {
                try {
                    foreach(var joint in plan.joints) {
                        var from=session.objects[joint.source]; var to=session.objects[joint.target].GetComponent<Rigidbody>();
                        Joint component; if(joint.kind=="fixed") component=from.AddComponent<FixedJoint>(); else { var hinge=from.AddComponent<HingeJoint>(); hinge.axis=joint.axis.normalized; hinge.useLimits=joint.limited; if(joint.limited) { var bounds=hinge.limits; bounds.min=joint.min; bounds.max=joint.max; hinge.limits=bounds; } component=hinge; }
                        component.autoConfigureConnectedAnchor=true; component.anchor=joint.anchor; component.connectedBody=to; component.enableCollision=false;
                    }
                    var relays = new HashSet<string>();
                    foreach (var b in plan.bindings) {
                        session.bindings.Add(b);
                        var capturedEvent=b; var subscription=EventRegistry.Attach(this,session,b,()=>Fire(session,capturedEvent));
                        if(subscription!=null) session.subscriptions.Add(subscription);
                        if ((b.on=="trigger" || b.on=="collision") && relays.Add(b.target)) { var relay=session.objects[b.target].AddComponent<TriggerRelay>(); relay.owner=this; relay.session=session; relay.target=b.target; }
                        if (b.on=="click") { var captured=b; session.objects[b.target].GetComponent<Button>().onClick.AddListener(() => { Fire(session,captured); var input=player ? player.GetComponent<PlayerBase>() : null; if (input) input.RefocusInput(); }); }
                    }
                    // Placement overlap validation is intentionally deferred to Phase 3.
                    // Structural validation and cancellation still precede activation.
                    if (cancelled() || session.disposed || shuttingDown) throw new OperationCanceledException("Cancelled.");
                    foreach (var root in new[]{session.frontRoot,session.worldRoot,session.cameraRoot,session.uiRoot}) {
                        root.SetActive(true);
                        if (cancelled() || session.disposed || shuttingDown) throw new OperationCanceledException("Cancelled during activation.");
                    }
                    sessions.Add(session); session.alive=true;
                    foreach (var b in session.bindings) if (b.on=="timer") session.timers.Add(StartCoroutine(Timer(session,b)));
                    if (cancelled() || session.disposed || shuttingDown) throw new OperationCanceledException("Cancelled during activation.");
                    pending=null; // Commit: cancellation after this point must not claim the creation was rolled back.
                } catch(Exception e) { failure="Could not activate creation: " + e.Message; }
            } else if (failure==null) failure="Cancelled.";
            if (failure!=null) { sessions.Remove(session); StopSessionTimers(session); session.Dispose(); if (ReferenceEquals(pending,session)) pending=null; completed(failure); yield break; }
            progress(1); completed(null);
        }
        static GameObject Root(string name, Transform parent, bool ui=false) { var go=ui ? new GameObject(name,typeof(RectTransform)) : new GameObject(name); go.SetActive(false); go.transform.SetParent(parent,false); if (ui) { var r=(RectTransform)go.transform; r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; r.localScale=Vector3.one; } return go; }
        void MakeItem(CreationSession s, Item x) {
            bool ui=x.kind=="label" || x.kind=="button"; GameObject go;
            if (ui) {
                go=new GameObject(x.id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); s.allocated.Add(go); go.SetActive(false); go.transform.SetParent(s.uiRoot.transform,false);
                var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=new Vector2(0.5f,0.5f); rect.anchoredPosition=new Vector2(x.position.x*10,x.position.y*10); rect.sizeDelta=new Vector2(Mathf.Clamp(x.scale.x*100,30,1000),Mathf.Clamp(x.scale.y*40,20,300));
                var image=go.GetComponent<Image>(); Color color; ColorUtility.TryParseHtmlString(x.color,out color); image.color=color; image.raycastTarget=x.kind=="button";
                if (x.kind=="button") { var button=go.AddComponent<Button>(); button.targetGraphic=image; }
                var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)); label.transform.SetParent(go.transform,false); var lr=(RectTransform)label.transform; lr.anchorMin=Vector2.zero; lr.anchorMax=Vector2.one; lr.offsetMin=lr.offsetMax=Vector2.zero; var t=label.GetComponent<Text>(); t.font=font; t.text=x.text; t.color=Color.black; t.alignment=TextAnchor.MiddleCenter; t.fontSize=18; t.raycastTarget=false;
            } else {
                if (x.kind=="light") { go=new GameObject(x.id); s.allocated.Add(go); go.SetActive(false); var light=go.AddComponent<Light>(); light.type=LightType.Spot; light.range=15; light.spotAngle=50; Color color; ColorUtility.TryParseHtmlString(x.color,out color); light.color=color; }
                else {
                    go=ObjectBuilderRegistry.Build(x.kind); if (!go) throw new InvalidOperationException("Builder returned no object."); s.allocated.Add(go); go.SetActive(false); go.name=x.id;
                    if (!go.GetComponent<Renderer>() || !go.GetComponent<Collider>()) throw new InvalidOperationException("Object builder must provide a Renderer and Collider.");
                    var collider=go.GetComponent<Collider>(); if (x.collider=="none") { collider.enabled=false; Destroy(collider); } else collider.isTrigger=x.collider=="trigger";
                    var renderer=go.GetComponent<Renderer>(); var shader=Shader.Find("Standard"); if (!shader && renderer.sharedMaterial) shader=renderer.sharedMaterial.shader; if (!shader) { Destroy(go); throw new InvalidOperationException("No compatible shader is available in this build."); }
                    var material=new Material(shader); Color color; ColorUtility.TryParseHtmlString(x.color,out color); material.color=color; renderer.sharedMaterial=material; s.materials.Add(material);
                    if (x.body!="none") { var rb=go.AddComponent<Rigidbody>(); rb.isKinematic=x.body=="kinematic"; rb.useGravity=x.body=="dynamic"; }
                }
                Transform parent = x.parent!="" ? s.objects[x.parent].transform : x.anchor=="camera" ? s.cameraRoot.transform : x.anchor=="world" ? s.worldRoot.transform : s.frontRoot.transform;
                go.transform.SetParent(parent,false); go.transform.localPosition=x.position; go.transform.localRotation=Quaternion.Euler(x.rotation); go.transform.localScale=x.scale;
            }
            go.SetActive(true); // Its transaction parent is still inactive.
            s.objects.Add(x.id,go); s.starts.Add(x.id,go.transform.localPosition); s.rotations.Add(x.id,go.transform.localRotation);
        }
        void Update() {
            if (!playerCamera) return;
            if (GameplayInputGate.BlocksGameplay) { if (hint) hint.text=""; return; }
            if (hint && Time.unscaledTime >= nextHint) { nextHint=Time.unscaledTime+0.2f; hint.text=""; RaycastHit h; if (Physics.Raycast(playerCamera.transform.position,playerCamera.transform.forward,out h,4f,Fragsurf.Movement.SurfPhysics.groundLayerMask,QueryTriggerInteraction.Ignore)) foreach(var s in sessions) if (s.alive) foreach(var b in s.bindings) if (b.on=="interact") { GameObject target; if (s.objects.TryGetValue(b.target,out target) && target && (h.transform==target.transform || h.transform.IsChildOf(target.transform))) hint.text="Press E to interact"; } }
            bool interact=Input.GetKeyDown(KeyCode.E);
            if (interact) {
                RaycastHit hit; if (Physics.Raycast(playerCamera.transform.position,playerCamera.transform.forward,out hit,4f,Fragsurf.Movement.SurfPhysics.groundLayerMask,QueryTriggerInteraction.Ignore)) {
                    foreach(var s in sessions) if (s.alive) foreach(var b in s.bindings) if (b.on=="interact") { GameObject target; if (s.objects.TryGetValue(b.target,out target) && target && (hit.transform==target.transform || hit.transform.IsChildOf(target.transform))) Fire(s,b); }
                }
            }
            foreach (KeyCode key in Keys) if (Input.GetKeyDown(key)) foreach(var s in sessions) if (s.alive) foreach(var b in s.bindings) if (b.on=="key" && b.key==key.ToString()) Fire(s,b);
        }
        public void Triggered(CreationSession s,string id,Collider other) { if (!s.alive || !player || !other || !other.transform.IsChildOf(player)) return; Dispatch(s,"trigger",id); }
        public void Collided(CreationSession s,string id) { if (s.alive) Dispatch(s,"collision",id); }
        void Dispatch(CreationSession s,string type,string id) { foreach(var b in s.bindings) if(b.on==type && b.target==id) Fire(s,b); }
        IEnumerator Timer(CreationSession s, Binding b) { while (s.alive) { yield return new WaitForSeconds(b.seconds); if (s.alive) Fire(s,b); } }
        bool ConditionsMet(CreationSession s,Binding b) { foreach(var c in b.conditions) if(!ConditionRegistry.Evaluate(c.op,s.variables[c.variable],c.value)) return false; return true; }
        void Error(string text) { if (lastErrorFrame==Time.frameCount) return; lastErrorFrame=Time.frameCount; if (report!=null) report(text); else Debug.LogWarning("AI creation: "+text); }
        void Fire(CreationSession s,Binding b) {
            if (!s.alive) return;
            if (s.dispatchFrame!=Time.frameCount) { s.dispatchFrame=Time.frameCount; s.dispatchCount=0; }
            if (++s.dispatchCount>s.budgets.dispatchPerFrame || s.depth>=32) { if (s.dispatchCount==s.budgets.dispatchPerFrame+1 || s.depth==32) Error("Behavior dispatch limit reached; event stopped."); return; }
            if(!ConditionsMet(s,b)) return;
            s.depth++;
            try { foreach(var a in b.actions) { if (a.delay>0) { if(s.pendingActions>=s.budgets.pendingActions) { Error("Pending action limit reached; delayed action skipped."); continue; } s.pendingActions++; StartCoroutine(Delayed(s,a)); } else Execute(s,a); } }
            finally { s.depth--; }
        }
        IEnumerator Delayed(CreationSession s,ActionSpec a) { yield return new WaitForSeconds(a.delay); s.pendingActions--; if(s.alive) Execute(s,a); }
        void Execute(CreationSession s,ActionSpec a) {
            try { GameObject go=null; if(ActionRegistry.TargetKind(a.type)=="object" && (!s.objects.TryGetValue(a.target,out go) || !go)) { Error("Behavior target no longer exists: "+a.target); return; } ActionRegistry.Execute(this,s,a,go); }
            catch(Exception e) { Error("Behavior error: "+e.Message); }
        }
        public void SetVariable(CreationSession s,string id,float value) { float n=Mathf.Clamp(value,-1000000,1000000); if(s.variables[id]==n) return; s.variables[id]=n; Dispatch(s,"change",id); }
        public void MoveAction(CreationSession s, ActionSpec a, GameObject go) { Coroutine old; if (s.movements.TryGetValue(a.target,out old) && old!=null) StopCoroutine(old); bool moving; s.moved.TryGetValue(a.target,out moving); s.moved[a.target]=!moving; s.movements[a.target]=StartCoroutine(Move(go.transform,s.starts[a.target]+(!moving ? a.offset : Vector3.zero),a.duration)); }
        public void RotateAction(CreationSession s,ActionSpec a,GameObject go) { string key="rotate:"+a.target; Coroutine old; if(s.movements.TryGetValue(key,out old) && old!=null) StopCoroutine(old); bool moved; s.moved.TryGetValue(key,out moved); s.moved[key]=!moved; var dest=s.rotations[a.target] * (moved ? Quaternion.identity : Quaternion.Euler(a.rotation)); s.movements[key]=StartCoroutine(Rotate(go.transform,dest,a.duration)); }
        IEnumerator Move(Transform t,Vector3 destination,float duration) { if (!t) yield break; Vector3 start=t.localPosition; float elapsed=0; while (t && elapsed<duration) { elapsed+=Time.deltaTime; t.localPosition=Vector3.Lerp(start,destination,Mathf.Clamp01(elapsed/duration)); yield return null; } if (t) t.localPosition=destination; }
        IEnumerator Rotate(Transform t,Quaternion destination,float duration) { if (!t) yield break; var start=t.localRotation; float elapsed=0; while(t && elapsed<duration) { elapsed+=Time.deltaTime; t.localRotation=Quaternion.Slerp(start,destination,Mathf.Clamp01(elapsed/duration)); yield return null; } if(t) t.localRotation=destination; }
        void OnDisable() { ShutdownForSceneRestart(); }
        void OnEnable() { shuttingDown=false; }
        void OnDestroy() { ShutdownForSceneRestart(); }
    }
}
