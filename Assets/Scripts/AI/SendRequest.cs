using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using RuntimeCreation;

// Runs before the EventSystem so Return is consumed while the InputField is still focused.
[DefaultExecutionOrder(-90)]
public class SendRequest : MonoBehaviour {
    [Header("Local Ollama")]
    public string baseUrl = "http://127.0.0.1:11434";
    public string model = "qwen2.5:7b";
    public bool useJsonMode = true;
    [Range(5,600)] public int timeoutSeconds = 180;
    [Header("Scene UI (optional except the player's input field)")]
    public Button sendButton, cancelButton, testButton;
    public Text connectionStatus, generationStatus, errorStatus, resultStatus, progressStatus, inputFeedback, interactionHint;
    [Header("Creation resource budgets")] public CreationBudgets creationBudgets = new CreationBudgets();
    PlayerBase player; CreationRuntime runtime; ILocalModelProvider provider;
    bool busy, cancel, initialized, shuttingDown; UnityAction sendListener, cancelListener, testListener;

    public void Initialize(PlayerBase p) {
        if (initialized) return;
        player=p;
        if (!player || !player.aiInput || !player.Crosshair) { Debug.LogError("AI setup requires PlayerBase input and crosshair references."); return; }
        var surf=player.GetComponent<Fragsurf.Movement.SurfCharacter>();
        var cam=surf && surf.viewTransform ? surf.viewTransform.GetComponentInChildren<Camera>() : null;
        var canvas=player.aiInput.GetComponentInParent<Canvas>();
        if (!canvas) { Debug.LogError("AI input must be inside a Canvas."); return; }
        if (creationBudgets==null) creationBudgets=new CreationBudgets();
        runtime=GetComponent<CreationRuntime>(); if (!runtime) runtime=gameObject.AddComponent<CreationRuntime>();
        runtime.Initialize(player.transform,cam,canvas,player.Crosshair.font,interactionHint,creationBudgets,RuntimeError);
        sendListener=SubmitFromButton;
        cancelListener=() => { Cancel(); if(player) player.RefocusInput(); };
        testListener=() => { if (isActiveAndEnabled && !shuttingDown) StartCoroutine(TestConnection()); if(player) player.RefocusInput(); };
        if(sendButton) sendButton.onClick.AddListener(sendListener);
        if(cancelButton) cancelButton.onClick.AddListener(cancelListener);
        if(testButton) testButton.onClick.AddListener(testListener);
        initialized=true;
        Set(connectionStatus,"Ollama: not tested"); Set(generationStatus,"Ready. F1 or tilde opens input."); Set(errorStatus,""); Set(resultStatus,""); Set(progressStatus,""); Set(inputFeedback,"");
    }
    static void Set(Text target,string value) { if (target) target.text=value; }
    void SetError(string message) { Set(errorStatus,message); if(!errorStatus && !string.IsNullOrEmpty(message)) Debug.LogWarning("AI: "+message); }
    void RuntimeError(string message) { SetError(message); }
    void Update() {
        if (!initialized || shuttingDown || !player || !player.aiInput) return;
        if (GameplayInputGate.Typing && player.aiInput.isFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) {
            Set(inputFeedback,"Enter detected while input focused.");
            TrySubmit();
        }
        if (busy && Input.GetKeyDown(KeyCode.F8)) Cancel();
    }
    void SubmitFromButton() {
        if (!player || !GameplayInputGate.Typing) { SetError("Open the AI input before sending."); return; }
        Set(inputFeedback,"Send button pressed."); TrySubmit();
    }
    void TrySubmit() {
        if (!isActiveAndEnabled || shuttingDown || !player || !player.aiInput || !runtime) { SetError("AI is not available; check scene references or restart state."); return; }
        if (busy) { SetError("A request or connection test is already running. Press F8 to cancel."); player.RefocusInput(); return; }
        string request=player.aiInput.text.Trim();
        if (request.Length==0) { SetError("Enter a request first."); player.RefocusInput(); return; }
        if (request.Length>2000) { SetError("Request is too long (maximum 2000 characters)."); player.RefocusInput(); return; }
        if (SafetyPolicy.Blocks(request)) { SetError("Request refused by the local safety filter."); player.RefocusInput(); return; }
        busy=true; cancel=false; SetError(""); Set(resultStatus,""); Set(progressStatus,"Preparing...");
        Set(inputFeedback,(inputFeedback ? inputFeedback.text + " " : "") + "Request accepted."); Set(generationStatus,"Request accepted; starting generation...");
        player.aiInput.text=""; player.deactivateInput(); StartCoroutine(Generate(request));
    }
    void Cancel() {
        if (!busy) { SetError("Nothing to cancel."); return; }
        if (cancel) return;
        cancel=true; if(provider!=null) provider.Cancel(); Set(generationStatus,"Cancelling...");
    }
    IEnumerator TestConnection() {
        if (busy) { SetError("A request or connection test is already running."); yield break; }
        busy=true; cancel=false; SetError(""); Set(connectionStatus,"Testing Ollama...");
        try { provider=new OllamaProvider(baseUrl,model,timeoutSeconds,useJsonMode); } catch(Exception e) { SetError(e.Message); Set(connectionStatus,"Ollama: configuration error"); busy=false; yield break; }
        string error=null; yield return provider.Test(e=>error=e); provider=null;
        if (cancel) { Set(connectionStatus,"Ollama test cancelled."); Set(generationStatus,"Cancelled."); }
        else if(error!=null) { Set(connectionStatus,"Ollama: unavailable"); SetError(error); }
        else Set(connectionStatus,"Ollama and configured model available.");
        busy=false;
    }
    IEnumerator Generate(string input) {
        try { creationBudgets.Validate(); provider=new OllamaProvider(baseUrl,model,timeoutSeconds,useJsonMode); } catch(Exception e) { SetError(e.Message); Set(generationStatus,"Generation failed."); busy=false; yield break; }
        string response=null,error=null,validationError=null; Plan plan=null;
        for (int attempt=0; attempt<2; attempt++) {
            Set(generationStatus,attempt==0 ? "Generating plan..." : "Correcting invalid plan..."); response=null; error=null;
            string prompt=CreationPrompt.Build(creationBudgets)+"\nPlayer request: "+input;
            if (attempt>0) prompt+="\nYour previous response failed validation: "+validationError+". Return a corrected complete JSON plan. Do not invent unsupported actions.";
            yield return provider.Generate(prompt,(r,e)=>{response=r;error=e;});
            if (cancel) { provider=null; Set(generationStatus,"Generation cancelled."); Set(progressStatus,""); busy=false; yield break; }
            if (error!=null) { provider=null; SetError(error); Set(generationStatus,"Generation failed."); Set(progressStatus,""); busy=false; yield break; }
            Set(generationStatus,"Validating plan...");
            try { plan=PlanReader.Read(response,creationBudgets); validationError=null; } catch(Exception e) { validationError=e.Message; }
            if (plan!=null) break;
        }
        provider=null;
        if (plan==null) { SetError("Invalid model plan: "+validationError); Set(generationStatus,"Generation failed."); Set(progressStatus,""); busy=false; yield break; }
        if (plan.items.Count==0) { Set(resultStatus,SafetyPolicy.Blocks(plan.message) ? "Request refused or unsupported; no objects created." : "No objects created: " + plan.message); Set(generationStatus,"No creation built."); Set(progressStatus,""); busy=false; yield break; }
        Set(generationStatus,"Building..."); Set(progressStatus,"Building 0%");
        bool buildFinished=false; error=null;
        yield return runtime.Build(plan,p=>Set(progressStatus,"Building " + Mathf.RoundToInt(p*100) + "%"),()=>cancel,e=>{error=e;buildFinished=true;});
        // A cancel received after commit must not be reported as a successful rollback.
        if (buildFinished && error==null) {
            Set(generationStatus,cancel ? "Generation completed before cancellation." : "Generation completed.");
            const string placementWarning="Placement overlap validation is unavailable; objects may intersect the player or environment.";
            Set(resultStatus,"Created " + plan.items.Count + " objects. " + (SafetyPolicy.Blocks(plan.message) ? "" : plan.message + " ") + placementWarning);
            if (!resultStatus) SetError(placementWarning);
            Debug.LogWarning("AI creation: " + placementWarning); Set(progressStatus,"Building 100%");
        } else {
            if (!buildFinished) { runtime.CancelPending(); error="Creation stopped without a completion result."; }
            if (error!=null && !cancel) SetError(error);
            Set(generationStatus,cancel ? "Generation cancelled; cleanup requested." : "Generation failed; cleanup requested.");
            Set(progressStatus,"");
        }
        busy=false;
    }
    void StopCurrentWork() {
        cancel=true;
        if(provider!=null) {
            try { provider.Cancel(); } catch(Exception e) { Debug.LogWarning("AI request cancellation failed: " + e.Message); }
            var disposable=provider as IDisposable;
            if(disposable!=null) try { disposable.Dispose(); } catch(Exception e) { Debug.LogWarning("AI request disposal failed: " + e.Message); }
            provider=null;
        }
        StopAllCoroutines();
        if(runtime) runtime.CancelPending();
        busy=false;
    }
    public void PrepareForSceneRestart() {
        shuttingDown=true;
        StopCurrentWork();
        // Committed sessions are cleaned up by CreationRuntime when the scene unloads.
    }
    public void ResumeAfterFailedSceneRestart() {
        shuttingDown=false;
        SetError("Scene restart could not start. Pending work was cancelled; committed creations were not deliberately removed.");
        Set(generationStatus,"Restart failed; ready for a new request.");
    }
    void OnDisable() {
        bool wasBusy=busy;
        StopCurrentWork();
        if (wasBusy) { Set(generationStatus,"Request stopped; staged cleanup requested. A committed creation may remain."); Set(progressStatus,""); }
    }
    void OnDestroy() {
        StopCurrentWork();
        if(sendButton && sendListener!=null) sendButton.onClick.RemoveListener(sendListener);
        if(cancelButton && cancelListener!=null) cancelButton.onClick.RemoveListener(cancelListener);
        if(testButton && testListener!=null) testButton.onClick.RemoveListener(testListener);
    }
}
