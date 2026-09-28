# C012: alloy combat and equipment shop

Authorized scope: implement blood equipment, fire alloy, decimal damage, equipment-shop categories/purchase feedback, conversation branches and tool purchases while reading Notion only. Existing provisional balance authorization remains applicable. Preserve scenes, prefabs, Inspector references, live editor state, unrelated edits and user saves. No commit or push.

## Read-only specification sources

- [Alloy effects](https://app.notion.com/p/3d9da1bd22db803381ffccc1e36c32e8): blood weapon/shield heal on hit/successful guard; weapon table 2; body armor retaliation by count 5/12/18/24. Fire ticks once per second; equal damage/duration refreshes remaining duration, different profiles coexist. Fire balance table is empty.
- [Combat](https://app.notion.com/p/3cada1bd22db80f98126ea26e5c1e911): incoming damage minus total armor defense times 0.5, rounded upward to one decimal place.
- [Equipment shop](https://app.notion.com/p/3e1da1bd22db80b0904fe57b4e0b305a): categories, selected row outline, paper details hidden without selection, insufficient-money button flash, immediate tool inventory delivery, bag capacity upgrades, owned upgrades inspectable but not purchasable, typewriter/Space, distinct purchased/browsed/cancel farewell text from diagrams.

## Implementation decisions within approved scope

- Assimilation remains health and offerings heal, as already selected by the user. Float health and damage preserve tenths through UI and JSON. Existing field names/IDs and integer JSON remain readable. New decimal saves are not guaranteed readable by older executables. Maximum health remains integer.
- Confirmed blood healing is 2. Shield uses the shared weapon/shield effect paragraph and the weapon value 2. Body armor excludes the Shield slot. Retaliation occurs once per accepted direct enemy hit; environmental damage has no attacker. Rolling prevents all incoming procs. Fatal hits cannot resurrect the player by shield healing.
- Existing provisional fire balance remains 3 damage/second for 5 seconds, Inspector configurable and labeled in equipment details. Identical profiles refresh duration without resetting the next tick; different profiles retain separate timers. Paused time does not advance fire. Death/disable clears active profiles. Burn and retaliation do not re-trigger weapon lifesteal.
- Projectiles capture weapon/ammunition alloy at launch, so the last arrow retains its effect after fallback. Fire/blood ammunition takes precedence over a non-alloy bow. Actual direct enemy hits carry their owner through both projectile families.
- Existing provisional shield reduction of 50%, prices and weapon base balance remain. Shop diagram prices are not silently treated as final balancing data. First-tier tools use the existing provisional base tool price; all T1–T3 tools are repeatable owned items with manual rack equipment. Bags remain sequential capacity upgrades with no physical bag item.
- Store farewell strings: purchased `다음에 또 오라고.`, browsed without purchase `다음번에는 네가 만진 것 전부 사가.`, initial cancel `그래, 필요한 게 있다면\n돈 두둑히 들고 다시 오도록 해.` The next visit resets receipt, category and transaction state. Pawnshop dialogue remains unchanged.

## Verification plan

Compile runtime and Editor sources; run an isolated Unity Play session including existing C011 physical smithy/item regression, actual monster damage, blood/guard/armor effects, fire refresh/coexistence/expiry, fractional save/reload, store filtering/outline/feedback/failure atomicity, bag ownership and all three dialogue branches. Inspect captured UI images. Hash all original inputs and user persistence before/after. Preserve any strict runner failure caused by isolated font-cache mutation and review its semantic delta separately. No player build or exhaustive enemy AI/physical mouse testing is claimed.


## Recorded verification — 2026-09-28

Unity 6000.3.11f1 isolated Play Mode: 85/85 checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Verified C011 physical smithy regression, real monster damage, blood/guard/body armor effects, fire refresh/coexistence/expiry, actual projectile collision and live burn Update, 0.6-health save/scene reload, shop filtering/selection/receipts/failed purchases/bag capacity and three farewell branches. Isolated Library reused with input hashes verified. Strict overall result remains FAILED: only generated glyph/character/atlas caches in two isolated dynamic font assets changed. Semantic cache review passed; source inputs and user persistence are preserved. No validation guard was relaxed.
