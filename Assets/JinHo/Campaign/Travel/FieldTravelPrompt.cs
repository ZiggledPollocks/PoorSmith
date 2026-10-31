// [코드 지도] FieldTravelPrompt: 필드 이동 대상의 확인 UI와 입력 차단을 처리한다.
// 주요 함수: Accept, Open, Close
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Travel/FieldTravelPrompt.cs.md

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The same authored Yes/No travel dialog is placed in both travel scenes.</summary>
public sealed class FieldTravelPrompt : MonoBehaviour
{
    GameUIController.Activity pauseActivity;
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
        var paper = modal?.transform.Find("TravelPaper")?.GetComponent<Image>();
        if (paper != null)
        {
            paper.color = new Color(.91f, .84f, .70f, 1f);
            TownFieldUiStyle.Frame(paper);
        }
        question.color = new Color(.17f, .13f, .09f, 1f);
        RuntimeUIFactory.FitText(question, question.fontSize);
        RuntimeUIFactory.FitText(error, error.fontSize);
        TownFieldUiStyle.FrameBehind(question, new Color(.95f,.89f,.76f,.96f));
        foreach (var button in new[] { yesButton, noButton })
        {
            button.image.color = new Color(.34f,.25f,.17f,.98f);
            TownFieldUiStyle.Frame(button.image);
        }
        TownFieldUiStyle.ContainText(modal.transform);
    }

    public bool Open(Func<bool> onYes, string message = null)
    {
        if (IsOpen || FieldSceneTravel.Busy || onYes == null) return false;
        pauseActivity?.Dispose();pauseActivity=GameUIController.Acquire(this,Cancel,true,true,800);
        
        accept = onYes;
        question.text = string.IsNullOrEmpty(message) ? "이동하시겠습니까?" : message;
        error.gameObject.SetActive(false);
        modal.SetActive(true);
        
        
        FindFirstObjectByType<PlayerInputHandler>()?.ClearGameplayInput();
        return true;
    }

    void Accept()
    {
        if (!IsOpen) return;
        var action = accept;
        string message = question.text;
        Close();
        if (action != null && !action())
        {
            Debug.LogWarning("Field scene travel could not start; check saved progress and Build Settings.");
            // Keep the player in the same scene and show a reviewable failure message.
            Open(() => action(), message);
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
        pauseActivity?.Dispose();pauseActivity=null;
        
        FindFirstObjectByType<PlayerInputHandler>()?.ClearGameplayInput();
    }

    void OnDestroy()
    {
        if (IsOpen)
        {
            pauseActivity?.Dispose();pauseActivity=null;
            
        }
        if (Active == this) Active = null;
    }
    void OnDisable(){pauseActivity?.Dispose();pauseActivity=null;}

}
