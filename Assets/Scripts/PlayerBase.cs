using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class PlayerBase : MonoBehaviour
{
    public Text Crosshair;
    public Text ErrorMessage;
    public InputField aiInput;
    public SendRequest sendRequest;
    public AudioSource errorSound;

    private bool _isInputOpen = false;
    private bool _restartPending = false;
    void Start()
    {
#if UNITY_EDITOR
        LogErrorSoundDiagnostic("Start.entry");
#endif
        GameplayInputGate.Typing = false;
        GameplayInputGate.SetApplicationFocus(Application.isFocused);
        SetNavigation(false);
        var inputNavigation = aiInput ? aiInput.navigation : default(Navigation);
        if (aiInput) { inputNavigation.mode = Navigation.Mode.None; aiInput.navigation = inputNavigation; }
        if (sendRequest is null)
        {
            StartCoroutine(ShowErrorMessage("SendRequest is null!"));
        }
        else
            sendRequest.Initialize(this);
    }

    void Update()
    {
        SetNavigation(GameplayInputGate.UIKeyboardNavigation);
        if (!Application.isFocused) return;
        if (_isInputOpen && !CanUseInput())
            CloseInput("input field, canvas, or EventSystem disabled");

        // The existing field remains visible and can also be selected directly.
        if (!_isInputOpen && GameplayInputGate.Context != GameInputContext.ModalDialog && aiInput && aiInput.isFocused)
        {
            Debug.Log("AI input: field selected directly.", this);
            activateInput();
        }

        if (!_isInputOpen && GameplayInputGate.Context == GameInputContext.UINavigating && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseNavigation();
            return;
        }
        if (!_isInputOpen && GameplayInputGate.Context != GameInputContext.ModalDialog && Input.GetKeyDown(KeyCode.Tab))
        {
            if (GameplayInputGate.Context == GameInputContext.UINavigating) CloseNavigation();
            else OpenNavigation();
            return;
        }
        if (GameplayInputGate.Context == GameInputContext.ModalDialog) return;
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log("AI input: detected F1.", this);
            if (_isInputOpen)
                CloseInput("F1");
            else
            {
                if (GameplayInputGate.Context == GameInputContext.UINavigating) CloseNavigation();
                activateInput();
            }
            return;
        }

        // Keep the alternate characters usable in requests while the field is open.
        if (!_isInputOpen && GameplayInputGate.Context == GameInputContext.Gameplay)
        {
            if (Input.GetKeyDown(KeyCode.Tilde))
            {
                Debug.Log("AI input: detected Tilde.", this);
                activateInput();
                return;
            }
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                Debug.Log("AI input: detected BackQuote.", this);
                activateInput();
                return;
            }
        }

        if (_isInputOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("AI input: detected Escape.", this);
            CloseInput("Escape");
            return;
        }

        if (!GameplayInputGate.BlocksGameplay && Input.GetKeyDown(KeyCode.R))
            RestartActiveScene();
    }

    private void RestartActiveScene()
    {
        if (_restartPending) return;
        // Single-mode restart is intentionally supported only with one settled scene.
        if (SceneManager.sceneCount != 1)
        {
            RestartError("Cannot restart: multiple scenes are loaded or a scene is still transitioning. Close additional scenes first.");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        var onlyScene = SceneManager.GetSceneAt(0);
        if (!scene.IsValid() || !scene.isLoaded || !onlyScene.IsValid() || !onlyScene.isLoaded || scene != onlyScene)
        {
            RestartError("Cannot restart: the active scene is unavailable or still transitioning.");
            return;
        }
        string path = scene.path;
        int index = scene.buildIndex;
        bool byPath = !string.IsNullOrEmpty(path) && Application.CanStreamedLevelBeLoaded(path);
        bool byIndex = !byPath && index >= 0 && Application.CanStreamedLevelBeLoaded(index);
        if (!byPath && !byIndex)
        {
            RestartError("Cannot restart the active scene: it has no loadable scene path or build index.");
            return;
        }
        _restartPending = true;
        try
        {
            // Only pending work is cancelled here. Committed sessions remain until scene unload.
            if (sendRequest) sendRequest.PrepareForSceneRestart();
            if (byPath) SceneManager.LoadScene(path, LoadSceneMode.Single);
            else SceneManager.LoadScene(index, LoadSceneMode.Single);
        }
        catch (System.Exception e)
        {
            _restartPending = false;
            if (sendRequest) sendRequest.ResumeAfterFailedSceneRestart();
            RestartError("Scene restart could not start. Pending work was cancelled; committed creations were not deliberately removed: " + e.Message);
        }
    }

    private void RestartError(string message)
    {
        Debug.LogWarning(message, this);
        if (ErrorMessage) StartCoroutine(ShowErrorMessage(message));
    }

    private bool CanUseInput()
    {
        if (!aiInput || !aiInput.isActiveAndEnabled || !aiInput.IsInteractable() || aiInput.readOnly || !aiInput.textComponent)
            return false;
        Canvas canvas = aiInput.GetComponentInParent<Canvas>();
        return canvas && canvas.isActiveAndEnabled && EventSystem.current && EventSystem.current.isActiveAndEnabled;
    }

    private void activateInput()
    {
        if (!CanUseInput())
        {
            Debug.LogWarning("AI input could not open: input, Canvas, or EventSystem is missing or disabled.", this);
            return;
        }

        _isInputOpen = true;
        GameplayInputGate.Typing = true;
        SetNavigation(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        FocusInput();
        StartCoroutine(EnsureInputFocusNextFrame());
        Debug.Log("AI input opened; gameplay input gated.", this);
    }

    private void FocusInput()
    {
        EventSystem.current.SetSelectedGameObject(aiInput.gameObject);
        aiInput.Select();
        aiInput.ActivateInputField();
    }

    public void RefocusInput()
    {
        if (_isInputOpen && CanUseInput()) FocusInput();
    }

    private IEnumerator EnsureInputFocusNextFrame()
    {
        yield return null;
        if (_isInputOpen && CanUseInput() && !aiInput.isFocused)
            FocusInput();
    }

    // SendRequest calls this on submission before starting generation.
    public void deactivateInput()
    {
        CloseInput("request submitted or UI closed");
    }

    private void CloseInput(string reason)
    {
        bool wasOpen = _isInputOpen;
        _isInputOpen = false;
        GameplayInputGate.Typing = false;
        SetNavigation(false);
        if (aiInput)
            aiInput.DeactivateInputField();
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject == (aiInput ? aiInput.gameObject : null))
            EventSystem.current.SetSelectedGameObject(null);
        if (!wasOpen)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("AI input closed (" + reason + "); gameplay input restored.", this);
    }

    private void SetNavigation(bool enabled)
    {
        if (EventSystem.current) EventSystem.current.sendNavigationEvents = enabled;
    }

    private void OpenNavigation()
    {
        if (!EventSystem.current || !EventSystem.current.isActiveAndEnabled) return;
        GameplayInputGate.SetContext(GameInputContext.UINavigating);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetNavigation(true);
        foreach (Selectable item in FindObjectsOfType<Selectable>())
        {
            if (item && item.isActiveAndEnabled && item.IsInteractable() && item.navigation.mode != Navigation.Mode.None && !(item is InputField))
            {
                EventSystem.current.SetSelectedGameObject(item.gameObject);
                break;
            }
        }
    }

    private void CloseNavigation()
    {
        GameplayInputGate.SetContext(GameInputContext.Gameplay);
        SetNavigation(false);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        GameplayInputGate.SetApplicationFocus(focused);
        if (!focused)
        {
            if (_isInputOpen) CloseInput("application lost focus");
            else if (GameplayInputGate.Context == GameInputContext.UINavigating) CloseNavigation();
            SetNavigation(false);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OnDisable()
    {
        if (GameplayInputGate.Context == GameInputContext.UINavigating) CloseNavigation();
        CloseInput("PlayerBase disabled");
    }

    private void OnDestroy()
    {
        GameplayInputGate.Typing = false;
    }

    public IEnumerator ShowErrorMessage(string message, float seconds = 0.1f)
    {
        Color color = ErrorMessage.color;

        if (color.a == 0)
        {
            color.a = 1;
            ErrorMessage.color = color;
        }

        ErrorMessage.text = message;
#if UNITY_EDITOR
        LogErrorSoundDiagnostic("ShowErrorMessage.beforeSoundCheck");
#endif
        if (errorSound)
#if UNITY_EDITOR
        {
            LogErrorSoundDiagnostic("ShowErrorMessage.beforePlay");
#endif
            errorSound.Play();
#if UNITY_EDITOR
        }
#endif

        while (color.a > 0)
        {
            color.a -= 0.01f;
            ErrorMessage.color = color;
            yield return new WaitForSeconds(seconds);
        }
        yield return null;
    }

#if UNITY_EDITOR
    private void LogErrorSoundDiagnostic(string stage)
    {
        const string prefix = "[ERROR SOUND DIAGNOSTIC]";
        string snapshot = prefix;
        try
        {
            AudioSource observedErrorSound = errorSound;
            bool referenceEqualsNull = object.ReferenceEquals(observedErrorSound, null);
            bool unityEqualsNull = observedErrorSound == null;
            bool clrIsNotNull = observedErrorSound is not null;
            bool playerBaseExists = this != null;
            snapshot += " stage=" + stage + " frame=" + Time.frameCount
                + " referenceEqualsNull=" + referenceEqualsNull
                + " unityEqualsNull=" + unityEqualsNull
                + " clrIsNotNull=" + clrIsNotNull
                + " playerBaseExists=" + playerBaseExists;

            string componentId = "<unavailable>", componentEnabled = "<unavailable>", componentActive = "<unavailable>";
            string ownerExists = "<unavailable>", ownerName = "<unavailable>", ownerActiveSelf = "<unavailable>", ownerActiveHierarchy = "<unavailable>";
            string sceneValid = "<unavailable>", sceneName = "<unavailable>", scenePath = "<unavailable>", sceneHandle = "<unavailable>", sceneLoaded = "<unavailable>";
            if (playerBaseExists)
            {
                componentId = GetInstanceID().ToString();
                componentEnabled = enabled.ToString();
                componentActive = isActiveAndEnabled.ToString();
                GameObject owner = gameObject;
                bool ownerIsValid = owner != null;
                ownerExists = ownerIsValid.ToString();
                if (ownerIsValid)
                {
                    ownerName = (owner.name ?? "<null-name>").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
                    ownerActiveSelf = owner.activeSelf.ToString();
                    ownerActiveHierarchy = owner.activeInHierarchy.ToString();
                    Scene ownerScene = owner.scene;
                    bool ownerSceneIsValid = ownerScene.IsValid();
                    sceneValid = ownerSceneIsValid.ToString();
                    if (ownerSceneIsValid)
                    {
                        sceneName = (ownerScene.name ?? "<null-name>").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
                        scenePath = (ownerScene.path ?? "<null-path>").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
                        sceneHandle = ownerScene.handle.ToString();
                        sceneLoaded = ownerScene.isLoaded.ToString();
                    }
                }
            }
            Debug.Log(snapshot
                + " playerBaseInstanceID=" + componentId
                + " enabled=" + componentEnabled + " isActiveAndEnabled=" + componentActive
                + " ownerExists=" + ownerExists + " gameObjectName=\"" + ownerName + "\""
                + " activeSelf=" + ownerActiveSelf + " activeInHierarchy=" + ownerActiveHierarchy
                + " sceneValid=" + sceneValid + " sceneName=\"" + sceneName + "\""
                + " scenePath=\"" + scenePath + "\" sceneHandle=" + sceneHandle + " sceneLoaded=" + sceneLoaded);
        }
        catch (System.Exception diagnosticException)
        {
            try
            {
                Debug.Log(snapshot + " stageRequested=" + stage
                    + " diagnosticReadFailure=" + diagnosticException.GetType().FullName);
            }
            catch (System.Exception)
            {
                // Even a failed diagnostic log must not escape into the original code.
            }
        }
    }
#endif
}
