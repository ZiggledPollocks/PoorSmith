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

    // Only an unfinished story save receives its missing tutorial essentials.
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
        // A continued unfinished tutorial prepares essentials once and retires grant keys.
        foreach(string key in new[]{"starting-axe","starting-pickaxe"})if(!story.applied.Contains(key))story.applied.Add(key);
        campaign.toolsLinked = campaign.starterSwordLinked = true;
        story.tutorialCompleted = true;
        story.step = "complete";
    }
}
