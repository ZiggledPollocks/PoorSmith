using System;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read-only field counterpart of the equipment rack's left panel.</summary>
public sealed class FieldEquipmentUI : MonoBehaviour
{
    BlacksmithView view;
    Canvas canvas;
    RectTransform modal;
    float previousTimeScale;
    bool previousExternalActivity;

    public void Initialize(TMP_FontAsset font)
    {
        if (canvas != null) return;
        var root = new GameObject("FieldEquipmentCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1160;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        view = root.AddComponent<BlacksmithView>();
        view.font = font != null ? font : TMP_Settings.defaultFontAsset;
        view.sprites = Array.Empty<Sprite>();
        modal = view.Full("FieldEquipmentModal", root.transform);
        var shade = modal.gameObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, .82f);
        shade.raycastTarget = true;
        modal.gameObject.SetActive(false);
    }

    public void Open()
    {
        var loop = SmithingLoop.Instance;
        if (modal == null || modal.gameObject.activeSelf || loop?.Initialized != true ||
            loop.InShop || GameUIController.BlocksGameplayInput) return;
        CombatHitFeedback2D.FinishHitStopBeforePause();
        previousTimeScale = Time.timeScale;
        previousExternalActivity = GameUIController.ExternalActivity;
        Time.timeScale = 0f;
        GameUIController.ExternalActivity = true;
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        modal.gameObject.SetActive(true);
        Draw(loop);
    }

    void Draw(SmithingLoop loop)
    {
        BlacksmithView.Clear(modal);
        var panel = view.Panel("EquippedBag", modal, new Vector2(.30f, .10f),
            new Vector2(.70f, .90f));
        panel.GetComponent<Image>().color = BlacksmithView.Dark;
        TownFieldUiStyle.Frame(panel.GetComponent<Image>());
        view.Text("BagTitle", panel, "장착 장비", 23,
            new Vector2(.10f, .67f), new Vector2(.64f, .73f));
        view.Button("CloseEquipment", panel, "닫기", new Vector2(.75f, .84f),
            new Vector2(.94f, .92f), Close);

        var portrait = view.Rect("PlayerPortrait", panel, new Vector2(.52f, .19f),
            new Vector2(.94f, .72f));
        portrait.gameObject.AddComponent<Image>().color = new Color(.67f, .47f, .35f);
        var character = view.Rect("PlayerIdle", portrait, new Vector2(.08f, .08f),
            new Vector2(.92f, .92f)).gameObject.AddComponent<Image>();
        var player = FindFirstObjectByType<PlayerAnimationController>();
        character.sprite = player != null ? player.GetComponentInChildren<SpriteRenderer>()?.sprite : null;
        character.color = character.sprite != null ? Color.white : new Color(.3f, .25f, .22f);
        character.preserveAspect = true;
        character.raycastTarget = false;

        string[] slots = { "Head", "Weapon", "Armor", "Shield", "Legs", "Pickaxe", "Feet", "Axe" };
        string[] names = { "머리", "무기", "몸통", "방패", "하의", "곡괭이", "신발", "도끼" };
        var data = loop.SmithData;
        var weapon = data.equipment.Find(x => x.slot == "Weapon");
        var definition = weapon == null ? null : loop.Catalog.Item(weapon.stack.itemId);
        if (definition != null && definition.bow) { slots[3] = "Arrow"; names[3] = "화살"; }
        bool hideOffhand = definition != null && definition.twoHanded && !definition.bow &&
            definition.id.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i == 3 && hideOffhand) continue;
            var entry = data.equipment.Find(x => x.slot == slots[i]);
            float x = i % 2 == 0 ? .105f : .325f;
            float y = .565f - (i / 2) * .125f;
            var art = entry == null ? "bag_slot" : view.ItemArtKey(loop.Catalog.Item(entry.stack.itemId));
            if (view.Art(art) == null) art = "bag_slot";
            var slot = view.Image("Equip_" + slots[i], panel, art,
                Color.white, new Vector2(x, y), new Vector2(x + .17f, y + .12f), true);
            TownFieldUiStyle.Frame(slot);
            if (entry == null)
                view.Text("EmptyLabel", slot.transform, names[i], 16, Vector2.zero,
                    Vector2.one, BlacksmithView.Ink, TextAlignmentOptions.Center);
            else
            {
                view.Text("EquipLabel", panel, names[i], 16,
                    new Vector2(x, y - .035f), new Vector2(x + .17f, y),
                    null, TextAlignmentOptions.Center);
                if (entry.stack.count > 1)
                    view.Text("Count", slot.transform, entry.stack.count.ToString(), 15,
                        new Vector2(.52f, 0f), Vector2.one, BlacksmithView.Cream,
                        TextAlignmentOptions.BottomRight);
            }
        }
        view.Text("Stats", panel, InventoryService.EquipmentStats(data, loop.Catalog), 17,
            new Vector2(.10f, .015f), new Vector2(.94f, .155f));
        TownFieldUiStyle.ContainText(panel);
    }

    void Update()
    {
        if (modal != null && modal.gameObject.activeSelf &&
            (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
             Gamepad.current?.buttonEast.wasPressedThisFrame == true)) Close();
    }

    public void Close()
    {
        if (modal == null || !modal.gameObject.activeSelf) return;
        modal.gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
        GameUIController.ExternalActivity = previousExternalActivity;
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }

    void OnDestroy() => Close();
}
