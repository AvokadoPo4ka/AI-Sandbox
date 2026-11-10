using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Opt-in manual diagnostic. Attach only during a test; it does not simulate input or claim pass/fail.
public sealed class InputAuditProbe : MonoBehaviour {
    KeyCode[] keys;
    void Awake() { keys=(KeyCode[])Enum.GetValues(typeof(KeyCode)); Debug.Log("InputAuditProbe active. Test with non-sensitive dummy text; disable after use."); }
    void Update() {
        foreach (var key in keys) {
            if (Input.GetKeyDown(key)) Debug.Log("InputAudit DOWN " + key + " context=" + GameplayInputGate.Context + " selected=" + Selected());
            if (Input.GetKeyUp(key)) Debug.Log("InputAudit UP " + key + " context=" + GameplayInputGate.Context);
        }
        if (Input.mouseScrollDelta.sqrMagnitude>0) Debug.Log("InputAudit wheel=" + Input.mouseScrollDelta);
    }
    string Selected() { return EventSystem.current && EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : "none"; }
    void OnApplicationFocus(bool focused) { Debug.Log("InputAudit application focus="+focused); }
}
