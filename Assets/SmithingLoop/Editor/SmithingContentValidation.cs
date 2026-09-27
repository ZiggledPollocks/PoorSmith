using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class SmithingContentValidation : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report)=>Validate();
    [MenuItem("Tools/Smithing/Validate integrated content")]
    public static void Validate()
    {
        var config=Resources.Load<SmithingLoopContent>("SmithingLoopContent");
        if(config==null||config.catalog==null||config.baseSword==null)throw new BuildFailedException("SmithingLoopContent: missing catalog or base sword");
        var ids=new HashSet<string>();
        foreach(var link in config.materials)
        {
            if(link?.fieldItem==null||string.IsNullOrWhiteSpace(link.fieldItem.ItemId)||!ids.Add(link.fieldItem.ItemId))throw new BuildFailedException("SmithingLoopContent: null/duplicate field item ID");
            if(link.fieldItem.Weight<0||float.IsNaN(link.fieldItem.Weight)||float.IsInfinity(link.fieldItem.Weight))throw new BuildFailedException(link.fieldItem.name+": invalid weight");
            if(!string.IsNullOrEmpty(link.smithItemId)&&config.catalog.Item(link.smithItemId)==null)throw new BuildFailedException(link.fieldItem.name+": unknown smith item "+link.smithItemId);
        }
        ids.Clear();foreach(var item in config.catalog.items)if(item==null||string.IsNullOrWhiteSpace(item.id)||!ids.Add(item.id))throw new BuildFailedException("Catalog: null/duplicate item ID");
        ids.Clear();foreach(var recipe in config.catalog.recipes.Where(r=>r.enabled))
        {
            if(string.IsNullOrWhiteSpace(recipe.id)||!ids.Add(recipe.id)||config.catalog.Item(recipe.outputId)==null||recipe.outputCount<=0)throw new BuildFailedException("Recipe invalid: "+recipe.id);
            foreach(var ingredient in recipe.ingredients)if(ingredient.count<=0||config.catalog.Item(ingredient.itemId)==null)throw new BuildFailedException(recipe.id+": invalid ingredient");
        }
        if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==config.shopScene))throw new BuildFailedException("Shop scene is missing from enabled build scenes");
        Debug.Log("SMITHING_CONTENT_VALIDATION_PASSED: "+config.materials.Length+" field IDs, "+ids.Count+" enabled recipes");
    }
}
