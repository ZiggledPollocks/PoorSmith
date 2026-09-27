using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith
{
    public enum Station { Workbench, Tools, Anvil, Quench, Furnace }
    public enum ToolKind { Knife, Plane, Saw, Whetstone, Hammer }
    public enum MaterialKind { Wood, Stone, Iron, Weapon, Armor, Other }
    public enum ItemGroup { Gathered, Crafted, Equipment }
    public enum Quality { Low, Medium, High, Finest, Master }
    public enum ScreenState { Home, Station, Selecting, Fuel, Playing, Animating, Result, Chest, Recipes, Codex, Sleep }
    [Serializable] public class ItemDefinition
    {
        public string id, displayName, description, sprite;
        public MaterialKind material;
        public ItemGroup group;
        public int price = 10, fuelValue;
        public float attack, defense, attackSpeed = 1;
        public bool heated, twoHanded, bow, arrow, canKnife;
        public string equipmentSlot, specialEffect;
    }
    [Serializable] public class Ingredient { public string itemId; public int count = 1; }
    [Serializable] public class RecipeDefinition
    {
        public string id, displayName, outputId;
        public string sourceUrl, sourceNote;
        public int outputCount = 1, maxStrokes;
        public bool enabled = true;
        public Station station;
        public ToolKind tool;
        public List<Ingredient> ingredients = new List<Ingredient>();
        public int strokes;
        public int[] anvilHits = new int[5];
        public bool symmetricAnvil;
    }
    [CreateAssetMenu(menuName = "Blacksmith/Catalog")]
    public class BlacksmithCatalog : ScriptableObject
    {
        public bool containsTestData = true;
        public string recipeSource;
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        public ItemDefinition Item(string id) { return items.Find(x => x.id == id); }
    }
    [Serializable] public class Stack
    {
        public string itemId; public int count; public Quality quality = Quality.High;
        public Stack(string id, int amount, Quality q = Quality.High) { itemId=id;count=amount;quality=q; }
        public string Key { get { return itemId+":"+(int)quality; } }
        public Stack Copy(int amount) { return new Stack(itemId, amount, quality); }
    }
    [Serializable] public class RecipeProgress { public string id; public int crafts; public int Level { get { return crafts >= 5 ? 2 : crafts >= 1 ? 1 : 0; } } }
    [Serializable] public class EquipmentEntry { public string slot; public Stack stack; }
    [Serializable] public class SaveData
    {
        public int version = 1, fuel, day=1, hp=65, maxHp=100;
        public bool night;
        public List<string> acquiredItems = new List<string>();
        public List<Stack> chest = new List<Stack>(), bag = new List<Stack>();
        public List<RecipeProgress> progress = new List<RecipeProgress>();
        public List<EquipmentEntry> equipment = new List<EquipmentEntry>();
    }
    public static class QualityRules
    {
        public static Quality FromAverage(float value) { return value >= 4 ? Quality.Finest : value >= 3 ? Quality.High : value >= 2 ? Quality.Medium : Quality.Low; }
        public static float Multiplier(Quality q) { return q==Quality.Low ? .5f : q==Quality.Medium ? .75f : q==Quality.Finest ? 1.25f : 1; }
        public static string Name(Quality q) { return new[]{"하급","중급","상급","최상급","장인급"}[(int)q]; }
    }
}
