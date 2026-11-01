using System;
using System.Collections.Generic;
using System.IO;
using SettingsMenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>The title scene's eight-row save list inside the gameplay settings tabs.</summary>
public sealed class InGameSaveHistoryTab : MonoBehaviour
{
    readonly List<(string path, string summary, bool valid)> choices = new();
    readonly Button[] rows = new Button[8];
    UIManager settings;
    GameObject panel;
    GameObject confirmation;
    TMP_Text confirmationText;
    Button confirmButton, cancelButton;
    int selected = -1;
    GameUIController.Activity confirmationActivity;
    GameObject previousSelection;

    public void Initialize(UIManager owner, RectTransform settingsRoot)
    {
        settings = owner;
        var tabsRoot = settingsRoot.Find("TopTabs") as RectTransform;
        var content = settingsRoot.Find("ContentArea") as RectTransform;
        var controller = settingsRoot.GetComponent<SettingsTabController>();
        var prefab = Resources.Load<GameObject>("UI/Screens/TitleSaveList");
        if (tabsRoot == null || content == null || controller == null || prefab == null)
        {
            Debug.LogError("The save history tab needs the settings tabs and TitleSaveList prefab.");
            Destroy(this);
            return;
        }

        panel = Instantiate(prefab, content, false);
        panel.name = "SaveHistoryPanel";
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.transform.Find("SaveListBack")?.gameObject.SetActive(false);
        panel.transform.Find("Title")?.GetComponent<TMP_Text>().SetText("저장본");
        confirmation = panel.transform.Find("LoadConfirmation")?.gameObject;
        if (confirmation == null) throw new InvalidOperationException("TitleSaveList has no LoadConfirmation.");
        // Keep the full-screen blocker outside the tab content viewport.
        confirmation.transform.SetParent(settingsRoot, false);
        confirmationText = TitleSceneController.PopupPart(confirmation, "NewGameMessage").GetComponent<TMP_Text>();
        confirmationText.text = "이 저장본으로 게임을 시작합니까";
        confirmButton = TitleSceneController.PopupPart(confirmation, "ConfirmNewGame").GetComponent<Button>();
        cancelButton = TitleSceneController.PopupPart(confirmation, "CancelNewGame").GetComponent<Button>();
        confirmButton.GetComponentInChildren<TMP_Text>(true).text = "예";
        cancelButton.GetComponentInChildren<TMP_Text>(true).text = "아니요";
        confirmButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(LoadSelected);
        cancelButton.onClick.AddListener(CancelSelection);
        confirmation.SetActive(false);
        for (int i = 0; i < rows.Length; i++)
        {
            int index = i;
            rows[i] = panel.transform.Find($"AutoSaveDay{i + 1}").GetComponent<Button>();
            rows[i].onClick.RemoveAllListeners();
            rows[i].onClick.AddListener(() => Select(index));
        }
        panel.SetActive(false);

        var tabs = new List<SettingsTabItem>(controller.Tabs);
        for (int i = 0; i < tabs.Count; i++)
        {
            var tabRect = (RectTransform)tabs[i].transform;
            tabRect.anchorMin = new Vector2(i * .25f + (i == 0 ? 0f : .005f), 0f);
            tabRect.anchorMax = new Vector2((i + 1) * .25f - .005f, 1f);
        }
        var tabObject = Instantiate(tabs[2].gameObject, tabsRoot, false);
        tabObject.name = "SaveHistoryTab";
        var newRect = (RectTransform)tabObject.transform;
        newRect.anchorMin = new Vector2(.755f, 0f);
        newRect.anchorMax = Vector2.one;
        newRect.offsetMin = newRect.offsetMax = Vector2.zero;
        var label = tabObject.GetComponentInChildren<TMP_Text>(true);
        label.text = "저장본";
        var item = tabObject.GetComponent<SettingsTabItem>();
        item.Configure(controller, 3, tabObject.GetComponent<Button>(),
            tabObject.GetComponent<Image>(), tabObject.GetComponent<Outline>(), label, panel);
        tabs.Add(item);
        controller.Configure(tabs, 0);
        controller.InitializeTabs();
        settings.OnSettingsOpened += Refresh;
        settings.OnSettingsClosed += CancelSelection;
        Refresh();
    }

    void Refresh()
    {
        choices.Clear();
        // UITestScene previews only its in-memory fixture and never reads the game's save directory.
        if (SceneManager.GetActiveScene().name == "UITestScene")
        {
            var smith = SmithingLoop.Instance?.SmithData;
            if (smith != null) choices.Add((null, $"{smith.day}일 · UI 테스트 저장본", false));
        }
        else
        {
            foreach (var entry in AutoSaveHistory.List(GameSavePaths.Root))
            {
                if (choices.Count == rows.Length) break;
                choices.Add(TitleSceneController.ReadChoice(entry.Path, entry.SavedAt));
            }
            var primary = GameSavePaths.File(AutoSaveHistory.PrimaryName);
            if (choices.Count == 0 && File.Exists(primary))
                choices.Add(TitleSceneController.ReadChoice(primary, File.GetLastWriteTime(primary)));
        }
        for (int i = 0; i < rows.Length; i++)
        {
            bool available = i < choices.Count;
            rows[i].gameObject.SetActive(true);
            rows[i].GetComponentInChildren<TMP_Text>(true).text =
                available ? choices[i].summary : "저장본이 없습니다";
            rows[i].interactable = available && choices[i].valid;
        }
        CancelSelection();
    }

    void Select(int index)
    {
        if (index < 0 || index >= choices.Count || !choices[index].valid) return;
        previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        selected = index;
        confirmationText.text = "이 저장본으로 게임을 시작합니까";
        confirmButton.gameObject.SetActive(true);
        confirmation.SetActive(true);
        RaiseConfirmation();
    }

    void RaiseConfirmation()
    {
        RuntimeUIFactory.RaiseModal((RectTransform)confirmation.transform);
        confirmationActivity?.Dispose();
        // Settings already owns pausing; this scope adds only its own input/cancel boundary.
        confirmationActivity = GameUIController.Acquire(this, CancelSelection, true, false, 30000);
        UIKeyboardSurface.Bind(this, confirmation.transform, "SaveHistoryConfirmation", CancelSelection, 30000);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(cancelButton.gameObject);
    }
    void CancelSelection()
    {
        bool wasOpen = confirmation != null && confirmation.activeSelf;
        selected = -1;
        if (confirmation != null) confirmation.SetActive(false);
        confirmationActivity?.Dispose(); confirmationActivity = null;
        if (wasOpen && previousSelection != null && previousSelection.activeInHierarchy &&
            previousSelection.GetComponent<Selectable>() is {} target && target.IsInteractable())
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }
    void Update(){if(confirmation != null && confirmation.activeSelf && !panel.activeInHierarchy)CancelSelection();}
    void OnDisable()=>CancelSelection();

    void LoadSelected()
    {
        int index = selected;
        CancelSelection();
        if (index < 0 || index >= choices.Count || !choices[index].valid) return;
        if (SmithingLoop.Instance != null && SmithingLoop.Instance.LoadHistoryFile(choices[index].path)) return;
        confirmationText.text = "저장본을 불러오지 못했습니다. 기존 진행은 유지됩니다.";
        confirmation.SetActive(true);
        RaiseConfirmation();
        confirmButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        CancelSelection();
        if (settings != null){settings.OnSettingsOpened -= Refresh; settings.OnSettingsClosed -= CancelSelection;}
        if (confirmation != null) Destroy(confirmation);
    }
}
