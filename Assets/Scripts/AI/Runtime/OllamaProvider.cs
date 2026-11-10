using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RuntimeCreation {
    public interface ILocalModelProvider {
        IEnumerator Test(Action<string> done);
        IEnumerator Generate(string prompt, Action<string,string> done);
        void Cancel();
    }
    public sealed class OllamaProvider : ILocalModelProvider, IDisposable {
        readonly string endpoint, model; readonly int timeout; readonly bool jsonMode; bool cancelled;
        UnityWebRequest active;
        [Serializable] sealed class Payload { public string model; public string prompt; public bool stream=false; }
        [Serializable] sealed class JsonPayload { public string model; public string prompt; public bool stream=false; public string format="json"; }
        sealed class LimitedDownload : DownloadHandlerScript {
            readonly MemoryStream buffer = new MemoryStream(); public bool exceeded;
            public LimitedDownload() : base(new byte[8192]) { }
            protected override bool ReceiveData(byte[] data,int length) { if (length<=0) return true; if (buffer.Length+length>262144) { exceeded=true; return false; } buffer.Write(data,0,length); return true; }
            public string Body { get { return Encoding.UTF8.GetString(buffer.ToArray()); } }
            public override void Dispose() { buffer.Dispose(); base.Dispose(); }
        }
        public OllamaProvider(string url,string modelName,int seconds,bool structuredJson) {
            Uri uri; if (!Uri.TryCreate(url,UriKind.Absolute,out uri) || (uri.Scheme!="http" && uri.Scheme!="https") || !uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) throw new ArgumentException("Endpoint must be an HTTP(S) loopback URL without credentials, query, or fragment.");
            if (string.IsNullOrWhiteSpace(modelName) || modelName.Length>100 || modelName.ToLowerInvariant().Contains("cloud")) throw new ArgumentException("Choose an installed local model (cloud model names are not allowed).");
            endpoint=url.TrimEnd('/'); model=modelName.Trim(); timeout=Mathf.Clamp(seconds,5,600); jsonMode=structuredJson;
        }
        public void Cancel() { cancelled=true; if (active!=null) active.Abort(); }
        // Called after cancellation when the owning MonoBehaviour is stopped or destroyed.
        public void Dispose() {
            cancelled=true;
            var request=active; active=null;
            if (request!=null) { try { request.Abort(); } finally { request.Dispose(); } }
        }
        IEnumerator Request(string path,string body,Action<string,string> done) {
            if (cancelled) { done(null,"Cancelled."); yield break; }
            UnityWebRequest request=null; string result=null, error=null; UnityWebRequestAsyncOperation op=null;
            try {
                try {
                    request=new UnityWebRequest(endpoint+path, body==null ? "GET" : "POST");
                    var download=new LimitedDownload(); request.downloadHandler=download;
                    if (body!=null) { request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)); request.SetRequestHeader("Content-Type","application/json"); }
                    request.timeout=timeout; request.redirectLimit=0; active=request;
                    op=request.SendWebRequest();
                    if (op==null) throw new InvalidOperationException("No HTTP operation was created.");
                } catch(Exception e) { error="Could not start local HTTP request: " + e.Message; }
                if (error==null) {
                    while (!op.isDone && !cancelled) yield return null;
                    if (cancelled) error="Cancelled.";
                    else {
                        var download=(LimitedDownload)request.downloadHandler;
                        result=download.Body;
                        if (download.exceeded) error="Ollama response exceeded the size limit.";
                        else if (request.result!=UnityWebRequest.Result.Success) {
                            if (request.responseCode==404) error="Ollama could not find the model or endpoint. Check the model name and installed models.";
                            else if (request.result==UnityWebRequest.Result.ConnectionError) error="Cannot reach Ollama, or the request timed out. Check that it is running and the endpoint is correct.";
                            else error="Ollama generation failed (HTTP " + request.responseCode + "): " + (request.error ?? "unknown error");
                        }
                    }
                }
            } finally {
                // The request can already have been disposed by the owner's shutdown path.
                if (request!=null && ReferenceEquals(active,request)) { active=null; request.Dispose(); }
                else if (request!=null && active==null && op==null) request.Dispose();
            }
            done(result,error);
        }
        public IEnumerator Test(Action<string> done) {
            string text=null,err=null; yield return Request("/api/tags",null,(t,e)=>{text=t;err=e;}); if (err!=null) { done(err); yield break; }
            try { var root=StrictJson.Parse(text,262144) as Dictionary<string,object>; object raw; if (root==null || !root.TryGetValue("models",out raw) || !(raw is List<object>)) throw new FormatException("Invalid model list."); bool found=false;
                foreach(var item in (List<object>)raw) { var m=item as Dictionary<string,object>; object name; if (m!=null && m.TryGetValue("name",out name) && name is string) { var n=(string)name; if (n==model || (model.IndexOf(':')<0 && n==model+":latest")) found=true; } }
                done(found ? null : "Ollama is reachable, but model '"+model+"' is not installed. Install it explicitly or choose another model.");
            } catch(Exception e) { done("Invalid Ollama model list: " + e.Message); }
        }
        public IEnumerator Generate(string prompt,Action<string,string> done) {
            string body=jsonMode ? JsonUtility.ToJson(new JsonPayload { model=model, prompt=prompt }) : JsonUtility.ToJson(new Payload { model=model, prompt=prompt }); string text=null,err=null;
            yield return Request("/api/generate",body,(t,e)=>{text=t;err=e;}); if(err!=null) { done(null,err); yield break; }
            try { var root=StrictJson.Parse(text,262144) as Dictionary<string,object>; object response,complete; if (root==null || !root.TryGetValue("response",out response) || !(response is string) || !root.TryGetValue("done",out complete) || !(complete is bool) || !(bool)complete || string.IsNullOrWhiteSpace((string)response)) throw new FormatException("Response was missing or incomplete."); done((string)response,null); }
            catch(Exception e) { done(null,"Invalid Ollama response: " + e.Message); }
        }
    }
}
