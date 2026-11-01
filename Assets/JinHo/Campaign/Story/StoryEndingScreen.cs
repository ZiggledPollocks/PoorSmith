using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum StoryEndingChoice { Continue, MainMenu }

/// <summary>Presentation-only ending card; the story host decides gameplay or title navigation.</summary>
public sealed class StoryEndingScreen : MonoBehaviour
{
    [SerializeField] TMP_Text title, thanks;
    [SerializeField] Button continueButton, mainMenuButton;
    GameUIController.Activity activity;
    public bool Chosen { get; private set; }
    public StoryEndingChoice Choice { get; private set; }

    public static StoryEndingScreen Open(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Story/StoryEndingScreen");
        if (prefab == null) throw new InvalidOperationException("Story/StoryEndingScreen is missing");
        var screen = Instantiate(prefab, parent, false).GetComponent<StoryEndingScreen>();
        // Keep the prefab's RectTransform editable; inherit the existing story CanvasScaler.
        var canvas = screen.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = 31500;
        screen.gameObject.AddComponent<GraphicRaycaster>();
        var credits = Resources.Load<TextAsset>("Story/Credits");
        var lines = credits != null ? credits.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        screen.title.text = lines.Length > 0 ? lines[0] : "Poor smith";
        screen.thanks.text = lines.Length > 1 ? lines[1] : "플레이 해주셔서 감사합니다";
        screen.Chosen = false;
        screen.continueButton.onClick.AddListener(screen.Continue);
        screen.mainMenuButton.onClick.AddListener(screen.MainMenu);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        // Explicit buttons own this choice. Esc must not dismiss it and leak input into the game.
        screen.activity = GameUIController.Acquire(screen, null, true, true, 31500);
        UIKeyboardSurface.Bind(screen,screen.transform,"EndingCard",null,31500);
        TownFieldUiStyle.PresentGameplay(screen.transform);
        return screen;
    }
    void Continue() => Choose(StoryEndingChoice.Continue);
    void MainMenu() => Choose(StoryEndingChoice.MainMenu);
    void Choose(StoryEndingChoice choice)
    {
        if (Chosen) return;
        Choice = choice; Chosen = true;
        continueButton.interactable = mainMenuButton.interactable = false;
    }
    public void Close()
    {
        gameObject.SetActive(false); Destroy(gameObject);
    }
    void OnDisable()
    {
        activity?.Dispose(); activity = null;
        continueButton.onClick.RemoveListener(Continue); mainMenuButton.onClick.RemoveListener(MainMenu);
    }
}
