// [코드 지도] BlacksmithSmokeCapture: 명시된 스모크 인수로만 자동 UI 조작과 화면 캡처를 수행한다.
// 주요 함수: Start, Capture, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/BlacksmithSmokeCapture.cs.md

using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blacksmith
{
    // Opt-in player smoke harness. Normal launches never execute this path.
    public class BlacksmithSmokeCapture : MonoBehaviour
    {
        void Awake()
        {
            if (Environment.GetCommandLineArgs().Contains("--blacksmith-smoke"))
                GetComponent<BlacksmithController>().loadSavedGame = false;
        }

        // 핵심 분기: !Environment.GetCommandLineArgs().Contains("--blacksmith-smoke") 판정.
        // 상태 변경: c.loadSavedGame 갱신.
        // 다음 연결: Blacksmith.BlacksmithSmokeCapture.Capture(string, string) 호출.
        IEnumerator Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("--blacksmith-smoke"))
                yield break;
            var c = GetComponent<BlacksmithController>();
            c.loadSavedGame = false;
            yield return null;
            yield return null;
            string dir = Path.Combine(Application.dataPath, Environment.GetCommandLineArgs().Contains("--blacksmith-small") ? "../Smoke1280" : "../Smoke");
            Directory.CreateDirectory(dir);
            yield return Capture(dir, "01-home");
            Click("Facility_1");
            yield return null;
            yield return Capture(dir, "02-workbench");
            Click("StationObject");
            yield return null;
            yield return Capture(dir, "03-selection");
            c.SetState(ScreenState.Chest);
            yield return null;
            yield return Capture(dir, "04-chest");
            c.SetState(ScreenState.Home);
            yield return null;
            Click("Facility_5");
            yield return null;
            Click("StationObject");
            yield return null;
            var ore = c.Inventory.Data.chest.Find(x => x.itemId == "ore");
            c.Inventory.Select(ore, Station.Furnace);
            c.SetState(ScreenState.Selecting);
            yield return null;
            yield return Capture(dir, "05-furnace");
            Click("StartCraft");
            yield return new WaitForSeconds(3.1f);
            if (c.State != ScreenState.Result)
                throw new Exception("Furnace failed to reach result");
            yield return Capture(dir, "06-result");
            Click("Collect");
            yield return null;
            c.SetState(ScreenState.Home);
            yield return null;
            Click("Facility_3");
            yield return null;
            Click("StationObject");
            yield return null;
            c.Inventory.Select(c.Inventory.Data.chest.Find(x => x.itemId == "hot_iron"), Station.Anvil);
            Click("StartCraft");
            yield return null;
            yield return Capture(dir, "07-anvil");
            Click("HammerPoint_4");
            Click("HammerPoint_4");
            Click("HammerPoint_4");
            Click("HammerPoint_4");
            Click("HammerPoint_4");
            yield return new WaitForSeconds(1.5f);
            if (c.State != ScreenState.Result)
                throw new Exception("Anvil failed to reach result");
            Click("Collect");
            yield return null;
            c.SetState(ScreenState.Home);
            yield return null;
            Click("Facility_4");
            yield return null;
            Click("StationObject");
            yield return null;
            c.Inventory.Select(c.Inventory.Data.chest.Find(x => x.itemId == "hot_plate"), Station.Quench);
            Click("StartCraft");
            yield return null;
            yield return Capture(dir, "08-quench");
            c.Crafting.Finish(true);
            c.SetState(ScreenState.Home);
            yield return null;
            Click("Facility_2");
            yield return null;
            Click("StationObject");
            yield return null;
            c.Inventory.Select(c.Inventory.Data.chest.Find(x => x.itemId == "wood"), Station.Tools);
            Click("StartCraft");
            yield return null;
            yield return Capture(dir, "09-tools");
            var input = FindFirstObjectByType<CraftGestureInput>();
            input.Upstroke();
            input.Upstroke();
            input.Upstroke();
            input.Upstroke();
            Click("FinishTool");
            yield return new WaitForSeconds(1.5f);
            if (c.State != ScreenState.Result)
                throw new Exception("Tool failed to reach result");
            Click("Collect");
            yield return null;
            c.SetState(ScreenState.Home);
            yield return null;
            Click("Facility_1");
            yield return null;
            Click("StationObject");
            yield return null;
            InventoryService.Add(c.Inventory.Data.chest, new Stack("blade", 1));
            InventoryService.Add(c.Inventory.Data.chest, new Stack("adhesive", 1));
            c.Inventory.Select(c.Inventory.Data.chest.Find(x => x.itemId == "blade"), Station.Workbench);
            c.Inventory.Select(c.Inventory.Data.chest.Find(x => x.itemId == "handle"), Station.Workbench);
            c.Inventory.Select(c.Inventory.Data.chest.Find(x => x.itemId == "adhesive"), Station.Workbench);
            Click("StartCraft");
            yield return null;
            yield return Capture(dir, "10-assembly");
            yield return new WaitForSeconds(1f);
            Click("TimingTarget");
            yield return new WaitForSeconds(1.4f);
            Click("TimingTarget");
            yield return new WaitForSeconds(1f);
            Click("TimingTarget");
            yield return new WaitForSeconds(1.5f);
            if (c.State != ScreenState.Result)
                throw new Exception("Assembly failed to reach result");
            Click("Collect");
            yield return null;
            Click("Recipes");
            yield return null;
            yield return Capture(dir, "11-recipes");
            c.Inventory.Equip(c.Inventory.Data.chest.Find(x => x.itemId == "bow"));
            c.Inventory.Equip(c.Inventory.Data.chest.Find(x => x.itemId == "arrow"));
            c.SetState(ScreenState.Chest);
            yield return null;
            Click("EquipmentTab");
            yield return null;
            yield return Capture(dir, "12-equipment");
            var equippedBow = c.Inventory.Data.equipment.Find(e => e.slot == "Weapon");
            InventoryService.Add(c.Inventory.Data.chest, new Stack("bow", 1));
            var source = FindObjectsByType<InventorySlotView>(FindObjectsSortMode.None).First(s => ReferenceEquals(s.Stack, equippedBow.stack));
            var target = FindObjectsByType<ItemDropTarget>(FindObjectsSortMode.None).First(t => t.name == "EmptySlot");
            InventorySlotView.Dragging = source;
            target.OnDrop(null);
            InventorySlotView.Dragging = null;
            if (c.Inventory.Data.equipment.Any(e => e.slot == "Weapon" || e.slot == "Arrow"))
                throw new Exception("Drag unequip failed");
            if (c.Inventory.Data.chest.Where(s => s.itemId == "bow").Sum(s => s.count) != 2)
                throw new Exception("Drag unequip lost stock");
            c.SetState(ScreenState.Home);
            yield return null;
            Click("Facility_1");
            yield return null;
            Click("StationObject");
            yield return null;
            var vest = c.catalog.recipes.First(r => r.outputId == "leather_vest");
            InventoryService.Add(c.Inventory.Data.chest, new Stack("leather", 2));
            InventoryService.Add(c.Inventory.Data.chest, new Stack("thread", 2));
            c.Inventory.FillRecipe(vest, 1);
            Click("StartCraft");
            yield return null;
            if (c.Crafting.Active)
                throw new Exception("Ambiguous recipe started without choosing output");
            yield return Capture(dir, "13-recipe-choice");
            Click("Choose_" + vest.id);
            yield return null;
            for (int n = 0; n < 4; n++)
            {
                yield return new WaitForSeconds(.8f);
                Click("TimingTarget");
            }

            yield return new WaitForSeconds(1.5f);
            if (c.State != ScreenState.Result || !c.Inventory.Data.chest.Any(s => s.itemId == "leather_vest" && s.count == 1))
                throw new Exception("Explicit vest recipe failed");
            Click("Collect");
            yield return null;
            foreach (var id in new[]
            {
                "leather_hat",
                "leather_vest",
                "leather_pants",
                "leather_shoes"
            }

            )
            {
                InventoryService.Add(c.Inventory.Data.chest, new Stack(id, 1));
                if (!c.Inventory.Equip(c.Inventory.Data.chest.First(s => s.itemId == id)))
                    throw new Exception("Armor equip failed: " + id);
            }

            if (c.Inventory.Data.equipment.Count(e => new[] { "Head", "Armor", "Legs", "Feet" }.Contains(e.slot)) != 4)
                throw new Exception("Armor slots replaced one another");
            c.SetState(ScreenState.Chest);
            yield return null;
            yield return Capture(dir, "14-armor-slots");
            File.WriteAllText(Path.Combine(dir, "smoke-result.txt"), "PASS: 14 screens rendered; Notion furnace, anvil, tool and assembly recipes; ambiguous output choice; separate armor slots; equipment drag and recipe views verified.\n");
            Application.Quit(0);
        }

        static void Click(string name)
        {
            var b = FindObjectsByType<Button>(FindObjectsSortMode.None).First(x => x.name == name);
            b.onClick.Invoke();
        }

        // 상태 변경: canvas.renderMode 갱신.
        static IEnumerator Capture(string dir, string name)
        {
            yield return new WaitForSeconds(.3f);
            var camera = Camera.main;
            var canvas = FindFirstObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            int width = Screen.width, height = Screen.height;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = old;
            rt.Release();
            Destroy(rt);
            Destroy(tex);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            yield return null;
        }
    }
}