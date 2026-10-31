using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>UI-only preview host: no gameplay services, rewards, save writer or scene transitions.</summary>
public sealed class EndingTestSceneController : MonoBehaviour
{
    [SerializeField] GameObject selection;
    [SerializeField] Button endingOne, endingTwo;
    [SerializeField] Sprite playerIdle;
    [SerializeField] TMP_FontAsset font;
    Coroutine playback;
    StoryTutorialView view;
    public bool Playing { get; private set; }
#if UNITY_EDITOR
    EditorGodModeButton debugButton;
    bool debugWasEnabled;
#endif

    void OnEnable()
    {
        endingOne.onClick.AddListener(PlayPaid);
        endingTwo.onClick.AddListener(PlayFailed);
#if UNITY_EDITOR
        SuppressDebugOverlay();
#endif
    }
    void Start()
    {
#if UNITY_EDITOR
        // The global Editor debug overlay is not part of this UI-only scene. Restore it on exit.
        SuppressDebugOverlay();
#endif
    }
#if UNITY_EDITOR
    void SuppressDebugOverlay()
    {
        if (debugButton != null) return;
        debugButton = FindFirstObjectByType<EditorGodModeButton>();
        if (debugButton != null) { debugWasEnabled = debugButton.enabled; debugButton.enabled = false; }
    }
#endif
    void PlayPaid() => Play(true);
    void PlayFailed() => Play(false);

    void Play(bool paid)
    {
        if (Playing) return;
        // Refuse an additive preview inside a live game; never reset or replace the live session.
        if (SmithingLoop.Instance != null || StoryTutorial.Instance != null || CampaignController.Instance != null)
        {
            Debug.LogWarning("EndingTestScene must be played independently, without a running game session.");
            return;
        }
        Playing = true; selection.SetActive(false);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        var root = new GameObject("Ending Dialogue", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        view = root.AddComponent<StoryTutorialView>();
        // Speaker context is presentation-only; no StoryProgress is constructed or mutated.
        view.Build(playerIdle, font, tutorialHud: false, hammerKnown: true);
        playback = StartCoroutine(Replay(paid));
    }
    IEnumerator Replay(bool paid)
    {
        yield return StoryEndingDialogue.Preview(view, paid);
        playback = null;
        Close();
    }
    void Update()
    {
        if (Playing && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
    }
    // Stop the sequence before closing its shared view, so no prior line/timer survives replay.
    public void Close()
    {
        if (playback != null) StopCoroutine(playback);
        playback = null; Playing = false;
        if (view != null)
        {
            view.CancelPresentation(); view.gameObject.SetActive(false); Destroy(view.gameObject); view = null;
        }
        if (selection != null) selection.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }
    void OnDisable()
    {
        if (endingOne != null) endingOne.onClick.RemoveListener(PlayPaid);
        if (endingTwo != null) endingTwo.onClick.RemoveListener(PlayFailed);
        Close();
#if UNITY_EDITOR
        if (debugButton != null) debugButton.enabled = debugWasEnabled;
        debugButton = null;
#endif
    }
}
