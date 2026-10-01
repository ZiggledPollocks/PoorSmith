// [코드 지도] CsvContentImporter: CSV 행을 읽어 콘텐츠 데이터와 드롭 구성을 갱신한다.
// 주요 함수: Apply, Parse, Read
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Editor/CsvContentImporter.cs.md

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Blacksmith;
using UnityEditor;
using UnityEngine;

// CSV is the editable source; Unity assets retain their GUIDs for scenes and saves.
public static class CsvContentImporter
{
    const string Root = "Assets/JinHo/Crafting/Data/Csv/";
    sealed class Row
    {
        readonly Dictionary<string, string> values;
        public readonly string file;
        public readonly int line;
        public Row(Dictionary<string, string> values, string file, int line) { this.values=values;this.file=file;this.line=line; }
        public string Get(string key) => values.TryGetValue(key,out var value) ? value : throw Error("missing column " + key);
        public int Int(string key) => int.TryParse(Get(key),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value) ? value : throw Error("invalid integer " + key);
        public float Float(string key) => float.TryParse(Get(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var value) && !float.IsNaN(value) && !float.IsInfinity(value) ? value : throw Error("invalid float " + key);
        public bool Bool(string key) => bool.TryParse(Get(key),out var value) ? value : throw Error("invalid boolean " + key);
        public T Enum<T>(string key) where T:struct
        {
            var n=Int(key);
            if(!System.Enum.IsDefined(typeof(T),n))throw Error("invalid enum " + key);
            return (T)System.Enum.ToObject(typeof(T),n);
        }
        public Exception Error(string message) => new InvalidDataException(file + ": row " + line + ": " + message);
    }
    sealed class FieldEdit { public ItemData asset; public Row row; }
    sealed class DropEdit { public ResourceData asset; public Row row; public int index, amount; }
    sealed class ToolEdit { public ToolData asset; public Row row; public int tier; }

    // 핵심 분기: catalog==null 판정.
    // 상태 변경: id 갱신.
    // 다음 연결: CsvContentImporter.Read(string) 호출.
    public static void Apply(BlacksmithCatalog catalog)
    {
        if(catalog==null)throw new ArgumentNullException(nameof(catalog));
        var itemRows=Read("items.csv");
        var hammerRates=new Dictionary<string,float>(StringComparer.Ordinal);
        foreach(var row in Read("weapon_kinds.csv"))
        {
            float rate=row.Float("attackRateMultiplier");
            if(row.Get("weaponKind")!="Hammer"||rate<.1f||rate>1f||!hammerRates.TryAdd(row.Get("id"),rate))
                throw row.Error("invalid or duplicate combat hammer category");
        }
        var recipeRows=Read("recipes.csv");
        var ingredientRows=Read("recipe_ingredients.csv");
        var fieldRows=Read("field_items.csv");
        var dropRows=Read("resource_drops.csv");
        var toolRows=Read("tools.csv");
        var items=new List<ItemDefinition>();
        var itemIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in itemRows)
        {
            string id=r.Get("id");
            if(string.IsNullOrWhiteSpace(id)||!itemIds.Add(id)||string.IsNullOrWhiteSpace(r.Get("displayName")))throw r.Error("empty or duplicate item ID/name " + id);
            string notionInterval=r.Get("notionAttackIntervalSeconds");
            if(!string.IsNullOrEmpty(notionInterval)&&r.Float("notionAttackIntervalSeconds")<=0)throw r.Error("attack interval must be positive");
            float attackSpeed=string.IsNullOrEmpty(notionInterval)?r.Float("attackSpeed"):1f/r.Float("notionAttackIntervalSeconds");
            var item=new ItemDefinition {
                id=id,displayName=r.Get("displayName"),description=r.Get("description"),sprite=r.Get("sprite"),
                material=r.Enum<MaterialKind>("material"),group=r.Enum<ItemGroup>("group"),
                price=r.Int("price"),buyPrice=r.Int("buyPrice"),fuelValue=r.Int("fuelValue"),attack=r.Float("attack"),defense=r.Float("defense"),attackSpeed=attackSpeed,
                shieldCooldownSeconds=r.Float("shieldCooldownSeconds"),shieldCooldownReduction=r.Float("shieldCooldownReduction"),
                heated=r.Bool("heated"),twoHanded=r.Bool("twoHanded"),bow=r.Bool("bow"),arrow=r.Bool("arrow"),canKnife=r.Bool("canKnife"),
                isHammerWeapon=hammerRates.ContainsKey(id),hammerAttackRateMultiplier=hammerRates.TryGetValue(id,out float hammerRate)?hammerRate:.75f,
                equipmentSlot=r.Get("equipmentSlot"),specialEffect=r.Get("specialEffect"),toolKind=r.Get("toolKind"),toolTier=r.Int("toolTier"),
                carryWeightKg=r.Float("carryWeightKg")
            };
            if(item.price<0||item.buyPrice<0||item.fuelValue<0||item.attackSpeed<0||item.toolTier<0||item.carryWeightKg<=0)throw r.Error("invalid item value");
            if(item.shieldCooldownSeconds<0||item.shieldCooldownReduction<0||item.shieldCooldownReduction>1)throw r.Error("invalid shield values");
            if(item.isHammerWeapon&&(item.material!=MaterialKind.Weapon||item.equipmentSlot!="Weapon"||item.bow))
                throw r.Error("combat hammer must be an equipped melee weapon");
            if(string.IsNullOrWhiteSpace(r.Get("source_status"))||string.IsNullOrWhiteSpace(r.Get("carryWeight_status")))throw r.Error("missing source status");
            items.Add(item);
        }
        foreach(var old in catalog.items)if(old!=null&&!itemIds.Contains(old.id))throw new InvalidDataException("CSV removes saved item ID: " + old.id);
        foreach(string id in hammerRates.Keys)if(!itemIds.Contains(id))throw new InvalidDataException("Unknown combat hammer ID: "+id);
        var recipes=new List<RecipeDefinition>();
        var recipeIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in recipeRows)
        {
            string id=r.Get("id");
            if(string.IsNullOrWhiteSpace(id)||!recipeIds.Add(id)||string.IsNullOrWhiteSpace(r.Get("displayName")))throw r.Error("empty or duplicate recipe ID/name " + id);
            var recipe=new RecipeDefinition {
                id=id,displayName=r.Get("displayName"),outputId=r.Get("outputId"),outputCount=r.Int("outputCount"),
                station=r.Enum<Station>("station"),tool=r.Enum<ToolKind>("tool"),strokes=r.Int("strokes"),maxStrokes=r.Int("maxStrokes"),
                anvilHits=Enumerable.Range(0,5).Select(i=>r.Int("anvilHit"+i)).ToArray(),
                symmetricAnvil=r.Bool("symmetricAnvil"),enabled=r.Bool("enabled"),sourceUrl=r.Get("sourceUrl"),sourceNote=r.Get("sourceNote")
            };
            if(!itemIds.Contains(recipe.outputId)||recipe.outputCount<=0||recipe.strokes<0||recipe.maxStrokes<0||recipe.anvilHits.Any(n=>n<0))throw r.Error("invalid recipe output or count");
            if(recipe.enabled&&recipe.station==Station.Anvil&&recipe.anvilHits.Sum()!=5)throw r.Error("invalid anvil hit total");
            if(string.IsNullOrWhiteSpace(r.Get("source_status")))throw r.Error("missing source status");
            recipes.Add(recipe);
        }
        var recipeById=recipes.ToDictionary(r=>r.id,StringComparer.Ordinal);
        foreach(var old in catalog.recipes)if(old!=null&&!recipeIds.Contains(old.id))throw new InvalidDataException("CSV removes saved recipe ID: " + old.id);
        var ingredientOrdinals=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var r in ingredientRows)
        {
            string id=r.Get("recipe_id");
            if(!recipeById.TryGetValue(id,out var recipe))throw r.Error("unknown recipe " + id);
            int ordinal=r.Int("ordinal"),count=r.Int("count");
            string itemId=r.Get("item_id");
            int expected=ingredientOrdinals.TryGetValue(id,out var next) ? next : 0;
            if(ordinal!=expected||count<=0||!itemIds.Contains(itemId))throw r.Error("invalid ingredient order, count, or item");
            recipe.ingredients.Add(new Ingredient{itemId=itemId,count=count});
            ingredientOrdinals[id]=expected+1;
        }
        if(recipes.Any(r=>r.ingredients.Count==0))throw new InvalidDataException("Recipe without ingredients");
        var fieldEdits=new List<FieldEdit>();
        var fieldPaths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in fieldRows)
        {
            string path=r.Get("asset_path");
            if(!path.StartsWith("Assets/JinHo/Gathering/Data/Items/",StringComparison.Ordinal)&&
               !path.StartsWith("Assets/JinHo/Gathering/Data/MonsterDrops/",StringComparison.Ordinal))throw r.Error("invalid field asset path");
            if(!fieldPaths.Add(path))throw r.Error("duplicate field asset path");
            var asset=AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if(asset==null||asset.ItemId!=r.Get("field_id")||!itemIds.Contains(r.Get("catalog_id")))throw r.Error("field asset or identity mismatch");
            if(r.Float("weight_kg")<0||r.Float("discount_assimilation_percent")<0)throw r.Error("negative field value");
            if(string.IsNullOrWhiteSpace(r.Get("source_status")))throw r.Error("missing source status");
            fieldEdits.Add(new FieldEdit{asset=asset,row=r});
        }
        var allFieldPaths=AssetDatabase.FindAssets("t:ItemData",new[]{"Assets/JinHo/Gathering/Data/Items","Assets/JinHo/Gathering/Data/MonsterDrops"})
            .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".asset",StringComparison.Ordinal)).ToArray();
        foreach(var path in allFieldPaths)if(!fieldPaths.Contains(path))throw new InvalidDataException(path+": field item asset missing from CSV");
        var toolEdits=new List<ToolEdit>();
        var toolPaths=new HashSet<string>(StringComparer.Ordinal);
        var legacyToolIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in toolRows)
        {
            string path=r.Get("asset_path"),legacyId=r.Get("legacy_tool_id"),catalogId=r.Get("catalog_id");
            if(!path.StartsWith("Assets/JinHo/Gathering/Data/Tools/",StringComparison.Ordinal)||
               !toolPaths.Add(path)||string.IsNullOrWhiteSpace(legacyId)||!legacyToolIds.Add(legacyId))throw r.Error("invalid or duplicate tool asset/ID");
            var asset=AssetDatabase.LoadAssetAtPath<ToolData>(path);
            var toolType=r.Enum<ToolType>("tool_type");
            if(asset==null||asset.ToolId!=legacyId||asset.ToolType!=toolType||string.IsNullOrWhiteSpace(r.Get("template_name")))throw r.Error("tool identity or type mismatch");
            int tier=1;
            if(!string.IsNullOrEmpty(catalogId))
            {
                var definition=items.Find(x=>x.id==catalogId);
                string kind=toolType==ToolType.Axe?"axe":toolType==ToolType.Pickaxe?"pick":null;
                if(definition==null||kind==null||definition.toolKind!=kind||definition.toolTier!=1)throw r.Error("invalid base catalog tool link");
                tier=definition.toolTier;
            }
            if(r.Float("damage")<=0||r.Float("reach")<=0||r.Float("attack_speed")<=0||
               r.Float("thrust_width")<=0||r.Float("swing_angle")<1||r.Float("swing_angle")>360||
               string.IsNullOrWhiteSpace(r.Get("source_status")))throw r.Error("invalid tool value");
            r.Enum<SwordAttackStyle>("sword_attack_style");
            toolEdits.Add(new ToolEdit{asset=asset,row=r,tier=tier});
        }
        var allToolPaths=AssetDatabase.FindAssets("t:ToolData",new[]{"Assets/JinHo/Gathering/Data/Tools"})
            .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".asset",StringComparison.Ordinal)).ToArray();
        foreach(var path in allToolPaths)if(!toolPaths.Contains(path))throw new InvalidDataException(path+": tool asset missing from CSV");
        var dropEdits=new List<DropEdit>();
        var dropNext=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var r in dropRows)
        {
            string path=r.Get("asset_path");
            if(!path.StartsWith("Assets/JinHo/Gathering/Data/",StringComparison.Ordinal))throw r.Error("invalid drop asset path");
            var asset=AssetDatabase.LoadAssetAtPath<ResourceData>(path);
            int index=r.Int("drop_index"),amount=r.Int("amount");
            int expected=dropNext.TryGetValue(path,out var next) ? next : 0;
            if(asset==null||index!=expected||index>=asset.DropCount||amount<=0)throw r.Error("drop asset, order, or amount mismatch");
            var prefab=asset.DropPrefabs[index];
            if(prefab==null||AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab))!=r.Get("prefab_guid"))throw r.Error("drop prefab GUID mismatch");
            if(string.IsNullOrWhiteSpace(r.Get("source_status")))throw r.Error("missing source status");
            dropEdits.Add(new DropEdit{asset=asset,row=r,index=index,amount=amount});
            dropNext[path]=expected+1;
        }
        foreach(var pair in dropNext)
        {
            var asset=AssetDatabase.LoadAssetAtPath<ResourceData>(pair.Key);
            if(asset.DropCount!=pair.Value)throw new InvalidDataException(pair.Key+": CSV does not cover every drop");
        }
        var allDropPaths=AssetDatabase.FindAssets("t:ResourceData",new[]{"Assets/JinHo/Gathering/Data"})
            .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".asset",StringComparison.Ordinal)).ToArray();
        foreach(var path in allDropPaths)if(!dropNext.ContainsKey(path))throw new InvalidDataException(path+": resource drop asset missing from CSV");
        // All CSV rows and asset references were checked before any mutation.
        catalog.items=items;
        catalog.recipes=recipes;
        catalog.recipeSource="Assets/JinHo/Crafting/Data/Csv/recipes.csv ("+recipes[0].sourceUrl+")";
        catalog.containsTestData=true;
        EditorUtility.SetDirty(catalog);
        foreach(var edit in fieldEdits)
        {
            var objectView=new SerializedObject(edit.asset);
            objectView.FindProperty("itemName").stringValue=edit.row.Get("item_name");
            objectView.FindProperty("description").stringValue=edit.row.Get("description");
            objectView.FindProperty("weight").floatValue=edit.row.Float("weight_kg");
            objectView.FindProperty("discountAssimilationRate").floatValue=edit.row.Float("discount_assimilation_percent");
            objectView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(edit.asset);
        }
        foreach(var edit in toolEdits)
        {
            var view=new SerializedObject(edit.asset);
            view.FindProperty("toolId").stringValue=edit.row.Get("legacy_tool_id");
            view.FindProperty("toolName").stringValue=edit.row.Get("template_name");
            view.FindProperty("toolType").enumValueIndex=edit.row.Int("tool_type");
            view.FindProperty("tier").intValue=edit.tier;
            view.FindProperty("damage").floatValue=edit.row.Float("damage");
            view.FindProperty("reach").floatValue=edit.row.Float("reach");
            view.FindProperty("attackSpeed").floatValue=edit.row.Float("attack_speed");
            view.FindProperty("swordAttackStyle").enumValueIndex=edit.row.Int("sword_attack_style");
            view.FindProperty("thrustWidth").floatValue=edit.row.Float("thrust_width");
            view.FindProperty("swingAngle").floatValue=edit.row.Float("swing_angle");
            view.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(edit.asset);
        }
        foreach(var edit in dropEdits)
        {
            var objectView=new SerializedObject(edit.asset);
            objectView.FindProperty("dropAmounts").GetArrayElementAtIndex(edit.index).intValue=edit.amount;
            objectView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(edit.asset);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"CSV_CONTENT_IMPORT_OK {items.Count} items, {recipes.Count} recipes, {ingredientRows.Count} ingredients, {fieldEdits.Count} field items, {toolEdits.Count} tools, {dropEdits.Count} drops");
    }

    static List<Row> Read(string name)
    {
        string path=Root+name;
        if(!File.Exists(path))throw new FileNotFoundException("CSV source missing",path);
        var records=Parse(File.ReadAllText(path,Encoding.UTF8),path);
        if(records.Count<2)throw new InvalidDataException(path+": no data rows");
        var headers=records[0];
        if(headers.Count!=headers.Distinct(StringComparer.Ordinal).Count()||headers.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException(path+": duplicate or empty header");
        var rows=new List<Row>();
        for(int i=1;i<records.Count;i++)
        {
            if(records[i].Count!=headers.Count)throw new InvalidDataException(path+": row "+(i+1)+" has incorrect column count");
            var values=new Dictionary<string,string>(StringComparer.Ordinal);
            for(int j=0;j<headers.Count;j++)values.Add(headers[j],records[i][j]);
            rows.Add(new Row(values,path,i+1));
        }
        return rows;
    }
    // 핵심 분기: value.Length>0&&value[0]=='\ufeff' 판정.
    // 상태 변경: value 갱신.
    static List<List<string>> Parse(string value,string path)
    {
        var records=new List<List<string>>();var fields=new List<string>();var field=new StringBuilder();bool quoted=false,closed=false;
        if(value.Length>0&&value[0]=='\ufeff')value=value.Substring(1);
        for(int i=0;i<value.Length;i++)
        {
            char c=value[i];
            if(quoted)
            {
                if(c=='"'&&i+1<value.Length&&value[i+1]=='"'){field.Append('"');i++;}
                else if(c=='"'){quoted=false;closed=true;}
                else field.Append(c);
            }
            else if(c=='"')
            {
                if(field.Length!=0||closed)throw new InvalidDataException(path+": malformed CSV quote");
                quoted=true;
            }
            else if(c==','||c=='\n'||c=='\r')
            {
                fields.Add(field.ToString());field.Clear();closed=false;
                if(c!=',')
                {
                    if(c=='\r'&&i+1<value.Length&&value[i+1]=='\n')i++;
                    records.Add(fields);fields=new List<string>();
                }
            }
            else
            {
                if(closed)throw new InvalidDataException(path+": trailing text after quote");
                field.Append(c);
            }
        }
        if(quoted)throw new InvalidDataException(path+": unterminated CSV quote");
        if(field.Length>0||fields.Count>0||closed){fields.Add(field.ToString());records.Add(fields);}
        return records;
    }
}
