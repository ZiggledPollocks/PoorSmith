using System;
using System.Collections.Generic;
using System.Linq;

// A read-only projection keeps original stack references so sorting never changes action identity.
public sealed class OwnedItemQuery
{
    public string text = "";
    public int sort, group = -1;
    public IEnumerable<T> Apply<T>(IEnumerable<T> source, Func<T,string> name,
        Func<T,int> count, Func<T,float> mass, Func<T,int> category)
    {
        var result = source.Where(x => (string.IsNullOrWhiteSpace(text) ||
            (name(x) ?? "").IndexOf(text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) &&
            (group < 0 || category(x) == group));
        return sort switch { 1 => result.OrderBy(name, StringComparer.CurrentCulture),
            2 => result.OrderByDescending(count), 3 => result.OrderBy(mass), _ => result };
    }
    public IEnumerable<Blacksmith.Stack> Apply(IEnumerable<Blacksmith.Stack> source, Blacksmith.BlacksmithCatalog catalog) =>
        Apply(source, s => catalog.Item(s.itemId)?.displayName ?? s.itemId, s => s.count,
            s => (catalog.Item(s.itemId)?.carryWeightKg ?? 0) * s.count,
            s => catalog.Item(s.itemId) is {} item ? (int)item.group : -1);
}
