using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PixelUI
{
    /// <summary>
    /// The demo's screens: an adventure (a HUD, a settings window, an inventory and a line of dialogue),
    /// the menus (pause, settings with switches and steppers, key bindings, save slots, a confirm box)
    /// and play (a bag and its item card, toasts, a quick-pick wheel, a hotbar, key prompts, dialogue).
    /// </summary>
    public partial class UIDemo
    {
        static readonly Color Background = new Color(0.07f, 0.07f, 0.1f);
        static readonly string[] Pages = { "Adventure", "Menus", "Play" };
        static readonly Vector2 CountAt = new Vector2(19, 10);

        static Texture2D Backdrop() => null;

        void Layout()
        {
            switch (Page(Pages))
            {
                case "Menus":
                    Menus();
                    break;
                case "Play":
                    Play();
                    break;
                default:
                    Hud();
                    Settings();
                    Inventory();
                    Dialog();
                    break;
            }
            Switcher(6, 220, 2);
            Screens(220, 2, Pages);
        }

        void Hud()
        {
            var hearts = Place(Row(screen, 0), 6, 5);
            foreach (var name in new[] { "heart", "heart", "heart", "heart_half", "heart_empty" })
                Icon(hearts, name);
            var bars = Place(Column(screen, 2), 92, 5);
            Bar(bars, "Red", 70, 90, 8);
            Bar(bars, "Blue", 45, 90, 8);
            var money = Place(Row(screen, 1), 300, 5);
            foreach (var (icon, text) in new[] { ("coin", "1250"), ("gem", "12"), ("key", "3") })
            {
                Icon(money, icon);
                Label(money, text);
                Gap(money, 4);
            }
        }

        void Settings()
        {
            var box = Window("SETTINGS", 6, 28, 148);
            Slider(box, "Music", 70, 34);
            Slider(box, "Sound", 40, 34);
            Check(box, "Fullscreen", true);
            Check(box, "Show hints", false);
            Dropdown(box, 1, "Easy", "Normal", "Hard");
            Input(box, "Hero name");
            var buttons = Row(box);
            foreach (var t in new[] { "Back", "Apply" })
                Expand(Button(buttons, t));
        }

        void Inventory()
        {
            var box = Window("INVENTORY", 162, 28, 258);
            var pages = Tabs(box, "Items", "Gear");
            var items = Grid(pages[0], 9, 2);
            var stock = new[] { ("potion", 3), ("potion_blue", 2), ("potion_green", 1), ("apple", 5), ("bomb", 4),
                ("key", 1), ("gem", 12), ("gem_red", 2), ("gem_green", 0), ("coin_silver", 30), ("star", 0),
                ("leaf", 7), ("flame", 0), ("lightning", 0), ("chest", 0), ("bag", 0), ("sword", 0), ("shield", 0) }
                .Where(p => Has(p.Item1)).Take(16).ToArray();
            for (int i = 0; i < 18; i++)
            {
                var slot = Make("InsetPanel", items);
                if (i >= stock.Length)
                    continue;
                var (name, count) = stock[i];
                Tip(Icon(slot, name), Title(name));
                var pad = slot.GetComponent<VerticalLayoutGroup>().padding;
                if (count > 1)
                    Over(slot, count.ToString(), new Vector2(pad.left + 19, pad.top + 10), true, null, Color.black);
            }
            foreach (var (icon, text) in new[] { ("sword", "Iron sword"), ("shield", "Oak shield") })
            {
                var r = Row(pages[1]);
                Icon(r, icon);
                Expand(Label(r, text));
                Button(r, "Equip");
            }
            var quest = Row(box);
            Icon(quest, "trophy");
            Label(quest, "Level 7");
            var xp = Bar(quest, "Gold", 60, 0, 8);
            Expand(xp);
            Centre((RectTransform)xp.transform);
            var actions = Row(box);
            foreach (var (icon, text) in new[] { ("check", "Use"), ("trash", "Drop"), ("info", "Info") })
                Expand(Button(actions, text, icon));
        }

        void Dialog()
        {
            var panel = Place(Make("Panel", screen), 6, 176);
            MinSize(panel, 414);
            var r = Row(panel, 6);
            Icon(Make("InsetPanel", r), "user");
            Expand(Label(r, "Welcome, traveller! The shop opens at dawn.\nBring coins, and mind the bombs."));
            Centre((RectTransform)Button(r, "", "arrow_right").transform);
        }

        void Menus()
        {
            var pause = Window("PAUSED", 6, 6, 84);
            OneOf(new[] { "Resume", "Save", "Load", "Settings", "Controls", "Quit" }.Select(t => MenuLine(pause, t, t == "Settings")).ToArray());
            var quit = Window("QUIT?", 6, 118, 84);
            Label(quit, "Progress since\nthe last save\nwill be lost.");
            var answer = Row(quit);
            foreach (var t in new[] { "Yes", "No" })
                Expand(Button(answer, t));

            var box = Window("SETTINGS", 96, 6, 160);
            Slider(box, "Music", 70, 44);
            Slider(box, "Sound", 40, 44);
            Switch(box, "Vsync", true);
            Switch(box, "Subtitles", false);
            Stepper(box, "Difficulty", new[] { "Easy", "Normal", "Hard" }, 1, 44);
            Stepper(box, "Window", new[] { "Windowed", "Fullscreen", "Borderless" }, 1, 44);
            Make("Separator", box);
            var buttons = Row(box);
            foreach (var t in new[] { "Back", "Apply" })
                Expand(Button(buttons, t));

            var keys = Window("CONTROLS", 262, 6, 159);
            foreach (var (action, caps) in new[] { ("Move", new[] { "W", "A", "S", "D" }), ("Jump", new[] { "Space" }), ("Use", new[] { "E" }), ("Pause", new[] { "Esc" }) })
            {
                var r = Row(keys, 2);
                Expand(Label(r, action));
                Prompt(r, "", caps);
            }

            var rebind = Window("PRESS A KEY", 96, 150, 160);
            Label(rebind, "Press a key for Jump").Text.alignment = TextAlignmentOptions.Top;
            Prompt(rebind, "Cancel", "Esc").GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;

            var saves = Window("LOAD GAME", 262, 100, 159);
            OneOf(SaveSlot(saves, "home", "Green Hills\nLv 7   2h 14m", true), SaveSlot(saves, "skull", "Old Crypt\nLv 4   1h 02m", false),
                SaveSlot(saves, "plus", "New game\n", false));
        }

        void Play()
        {
            var bag = Window("BAG", 6, 6, 0);
            var grid = Grid(bag, 6, 2);
            var stock = new[] { ("sword", 1), ("potion_blue", 3), ("shield", 1), ("apple", 5), ("bomb", 4), ("key", 2), ("gem", 12),
                ("potion", 2), ("coin_silver", 30), ("leaf", 7) };
            for (int i = 0; i < 18; i++)
                ItemSlot(grid, i < stock.Length ? stock[i].Item1 : "", i < stock.Length ? stock[i].Item2 : 0, i == 1);

            var card = Place(Card(), 176, 6);
            MinSize(card, 110);
            card.GetComponent<VerticalLayoutGroup>().spacing = 3;
            var top = Row(card, 3);
            Icon(top, "potion_blue");
            Centre((RectTransform)Label(top, "Blue potion", "Nameplate").transform);
            Label(card, "Restores 40 mana\nover five seconds.");
            var worth = Row(card, 2);
            Icon(worth, "coin");
            Label(worth, "15");
            var use = Row(card, 6);
            Prompt(use, "Use", "E");
            Prompt(use, "Drop", "Q");

            var toasts = Column(screen, 2);
            Fit(toasts);
            toasts.anchorMin = toasts.anchorMax = toasts.pivot = Vector2.one;
            toasts.anchoredPosition = new Vector2(-6, -6);
            foreach (var (icon, text) in new[] { ("star", "Quest updated"), ("coin", "+50 coins"), ("save", "Game saved") })
                Toast(toasts, icon, text);

            Radial(new Vector2(362, 146), 44, new[] { "sword", "shield", "bomb", "potion", "potion_blue", "apple", "key", "gem" }, 3, "Red potion", 60, "Q");

            var talk = Place(Make("Panel", screen), 6, 112);
            MinSize(talk, 290);
            var r = Row(talk, 6);
            Portrait(r, "user");
            var said = Column(r, 2);
            Expand(said);
            Label(Row(said, 0), "Old Miner", "Nameplate");
            Label(said, "The east tunnel caved in. Bring me\nthree bombs and I will clear it.");
            Prompt(said, "Next", "E").GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperRight;

            var hotbar = Place(Make("Panel", screen), 6, 176);
            var keys = Row(hotbar, 2);
            var items = new[] { "sword", "shield", "bomb", "potion", "potion_blue", "apple", "key", "gem" };
            for (int i = 0; i < items.Length; i++)
                Corner(ItemSlot(keys, items[i], 1, i == 2), (i + 1).ToString(), new Vector2(-2, -3), false);

            var hints = Place(Column(screen, 2), 232, 180);
            Prompt(hints, "Bag", "Tab");
            Prompt(hints, "Menu", "Esc");
        }
    }
}
