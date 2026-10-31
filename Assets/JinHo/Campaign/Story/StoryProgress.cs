using System;
using System.Linq;
using System.Collections.Generic;
using Blacksmith;

/// <summary>Optional version-1 save extension. Legacy saves have no story.</summary>
[Serializable]
public sealed class StoryProgress
{
    public bool enabled, tutorialCompleted, endingSeen;
    public int startingDay = 1;
    public int safeScene = 1; // Next safe story segment; optional for legacy version-1 saves.
    public int deliveryTarget; // Fixed once when the delivery instruction starts; optional save field.
    public string step = "intro";
    public List<string> applied = new();
    public const int DebtGold = 1500;

    public static void RemovePawnDebt(CampaignState campaign)
    {
        if (campaign.pawnDebtRemoved) return;
        // Preserve old collateral as recoverable stock; no new loan or interest is created.
        var incoming = (campaign.pawnLoans ?? new List<PawnLoan>()).Where(l => l?.collateral != null).Select(l => l.collateral).ToArray();
        foreach (var group in incoming.GroupBy(s => s.Key))
            if (campaign.pawnStock.Where(s => s.Key == group.Key).Sum(s => (long)s.count) + group.Sum(s => (long)s.count) > int.MaxValue)
                throw new InvalidOperationException("전당포 보관 수량이 너무 많아 이전 대출을 변환할 수 없습니다.");
        foreach (var stack in incoming) InventoryService.Add(campaign.pawnStock, stack.Copy(stack.count));
        campaign.pawnLoans?.Clear(); campaign.pawnDebtRemoved = true;
    }

    // Legacy saves stored the task name but no safe-segment field. Infer conservatively
    // from actual completed recipe records; this migration never creates rewards.
    public static void PrepareResume(CampaignState campaign,SaveData data,BlacksmithCatalog catalog)
    {
        var story=campaign.story;if(story==null||!story.enabled||story.tutorialCompleted||story.safeScene>1||story.step=="intro")return;
        int Crafts(string id)=>catalog.recipes.Where(r=>r.enabled&&r.outputId==id).Sum(r=>data.progress?.FirstOrDefault(p=>p.id==r.id)?.crafts??0);
        void Mark(string key){key="checkpoint:"+key;if(!story.applied.Contains(key))story.applied.Add(key);}
        int planks=Crafts("plank");if(planks>0)Mark("first-plank");if(planks>1)Mark("second-plank");
        if(Crafts("handle")>0)Mark("handle");if(Crafts("wood_long_blade")>0||story.applied.Contains("blade-fallback"))Mark("blade");if(Crafts("wood_sword")>0)Mark("wood-sword");
        story.safeScene=story.step switch
        {
            "rack"=>9,"forest"=>11,"tree" or "gather" or "altar" or "offering" or "home" or "delivery-entrance" or "delivery" or "home-smithy"=>12,
            "sleep"=>13,"door"=>7,"assembly" or "assembly-open" or "workbench" or "craft-wood_sword" or "working-wood_sword" or "collect-wood_sword"=>7,
            "craft-wood_long_blade" or "working-wood_long_blade" or "collect-wood_long_blade"=>6,
            "saw" or "wood" or "recipe" or "recipe-node" or "recipe-fill" or "knife" or "blade" or "craft-plank" or "working-plank" or "collect-plank" or "craft-handle" or "working-handle" or "collect-handle"=>5,
            _=>4
        };
        if(story.safeScene==12)
        {
            if(story.step!="tree")Mark("tree");
            if(story.step!="tree"&&story.step!="gather")Mark("gather");
            if(story.step=="home"||story.step.StartsWith("delivery")||story.step=="home-smithy")Mark("offering");
            if(story.step.StartsWith("delivery")||story.step=="home-smithy")Mark("home");
            if(story.step=="home-smithy")Mark("delivery");
        }
    }
    // Only a confirmed tutorial skip awards missing essentials and completes the story.
    // Existing owned stacks and the completion flag prevent repeated essential grants.
    public static void CompleteForContinue(CampaignState campaign, SaveData data, BlacksmithCatalog catalog)
    {
        var story = campaign.story;
        if (story == null || !story.enabled || story.tutorialCompleted) return;
        foreach (string id in new[] { "wood_sword", "town_axe_1", "town_pick_1" })
        {
            if(id=="town_pick_1"&&story.applied.Contains("starting-pickaxe"))continue;
            if (catalog.Item(id) == null) throw new InvalidOperationException("Missing tutorial item: " + id);
            if (data.chest.Concat(data.bag).Any(s => s.itemId == id) ||
                data.equipment.Any(e => e.stack.itemId == id)) continue;
            InventoryService.Add(data.chest, new Blacksmith.Stack(id, 1));
            if (!data.acquiredItems.Contains(id)) data.acquiredItems.Add(id);
        }
        // Confirmed final skip prepares essentials once and retires grant keys.
        foreach(string key in new[]{"starting-axe","starting-pickaxe"})if(!story.applied.Contains(key))story.applied.Add(key);
        campaign.toolsLinked = campaign.starterSwordLinked = true;
        story.tutorialCompleted = true;
        story.step = "complete";
    }
}
