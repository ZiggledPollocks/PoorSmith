using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The same authored Yes/No travel dialog is placed in both travel scenes.</summary>
public sealed class FieldTravelPrompt : MonoBehaviour
{
    public static FieldTravelPrompt Active { get; private set; }
    [SerializeField] GameObject modal;
    [SerializeField] TMP_Text question;
    [SerializeField] Button yesButton;
    [SerializeField] Button noButton;
    [SerializeField] TMP_Text error;

    Func<bool> accept;
    float previousTimeScale;
    bool previousExternalActivity;
    public bool IsOpen => modal != null && modal.activeSelf;

    void Awake()
    {
        Active = this;
        if (modal != null) modal.SetActive(false);
        yesButton.onClick.AddListener(Accept);
        noButton.onClick.AddListener(Cancel);
    }

    public bool Open(Func<bool> onYes)
    {
        if (IsOpen || FieldSceneTravel.Busy || onYes == null) return false;
        previousTimeScale = Time.timeScale;
        previousExternalActivity = GameUIController.ExternalActivity;
        accept = onYes;
        question.text = "이동하시겠습니까?";
        error.gameObject.SetActive(false);
        modal.SetActive(true);
        GameUIController.ExternalActivity = true;
        Time.timeScale = 0f;
        FindFirstObjectByType<PlayerInputHandler>()?.ClearGameplayInput();
        return true;
    }

    void Accept()
    {
        if (!IsOpen) return;
        var action = accept;
        Close();
        if (action != null && !action())
        {
            Debug.LogWarning("Field scene travel could not start; check saved progress and Build Settings.");
            // Keep the player in the same scene and show a reviewable failure message.
            Open(() => action());
            error.text = "이동할 수 없습니다. 저장 상태와 맵 등록을 확인하세요.";
            error.gameObject.SetActive(true);
        }
    }

    public void Cancel() => Close();

    void Close()
    {
        if (!IsOpen) return;
        accept = null;
        modal.SetActive(false);
        Time.timeScale = previousTimeScale;
        GameUIController.ExternalActivity = previousExternalActivity;
        FindFirstObjectByType<PlayerInputHandler>()?.ClearGameplayInput();
    }

    void OnDestroy()
    {
        if (IsOpen)
        {
            Time.timeScale = previousTimeScale;
            GameUIController.ExternalActivity = previousExternalActivity;
        }
        if (Active == this) Active = null;
    }
}
