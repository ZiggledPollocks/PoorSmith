using System.Collections.Generic;
using UnityEngine;

namespace Blacksmith
{
    // Coordinates follow the four diagrams in the Notion crafting document.
    // Item IDs, rather than material enums, keep equipment in its resource tree.
    public static class RecipeGraphLayout
    {
        public sealed class Node
        {
            public readonly string ItemId;
            public readonly Vector2 Position;
            public Node(string id, float x, float y) { ItemId=id; Position=new Vector2(x*210+100,y*112+70); }
        }
        public static readonly string[] Tabs={"나무","돌","철","전리품"};
        public static List<Node> Nodes(int tab)
        {
            switch(tab)
            {
                case 0: return new List<Node> {
                    new Node("wood",0,2), new Node("branch",0,5), new Node("vine",3,0),
                    new Node("plank",1,1), new Node("smooth_wood",1,3),
                    new Node("fiber",2,0), new Node("thread",3,1), new Node("rope",4,0),
                    new Node("wood_long_blade",2,1), new Node("wood_sword",4,1),
                    new Node("wood_shield",2,2), new Node("wood_block",2,3),
                    new Node("handle",3,2.5f), new Node("wood_short_blade",3,3.5f),
                    new Node("wood_dagger",4,3.5f), new Node("wood_hammer",2,4),
                    new Node("trimmed_branch",1,5),new Node("bow",2,5),new Node("sharp_branch",2,6) };
                case 1: return new List<Node> {
                    new Node("stone",0,2),new Node("stone_slab",1,1),new Node("stone_long_blade",2,0),
                    new Node("stone_sword",3,0),new Node("stone_slab_piece",2,1),
                    new Node("stone_short_blade",3,1),new Node("stone_dagger",4,1),
                    new Node("stone_shield",2,2),new Node("trimmed_stone",1,3.5f),
                    new Node("small_stone",2,3),new Node("stone_arrowhead",3,3),
                    new Node("stone_arrow",4,3),new Node("stone_hammer",2,4.5f) };
                case 2: return new List<Node> {
                    new Node("ore",0,3),new Node("hot_iron",1,3),
                    new Node("hot_plate",2,1),new Node("plate",3,1),
                    new Node("iron_head",4,0),new Node("armor",4,1),new Node("iron_legs",4,2),
                    new Node("iron_feet",5,0),new Node("shield",5,1),
                    new Node("iron_hot_ingot",2,2),new Node("iron_ingot",3,2),
                    new Node("iron_hot_long",2,3),new Node("blade",3,3),new Node("sword",4,3),
                    new Node("iron_sharp_long",4,4),new Node("iron_sharp_sword",5,3),
                    new Node("iron_hot_short",2,5),new Node("iron_short",3,5),
                    new Node("iron_dagger",4,5),new Node("iron_sharp_short",4,6),
                    new Node("iron_sharp_dagger",5,5),new Node("iron_arrowhead",5,6),new Node("arrow",6,6),
                    new Node("ingot",2,7),new Node("iron_trimmed_lump",3,7),
                    new Node("warhammer",3,8),new Node("iron_fine_hammer",4,7.5f),
                    new Node("blood_hot",1,9),new Node("steel_hot",2,9) };
                default: return new List<Node> {
                    new Node("raw_leather",0,1.5f),new Node("leather_prepared",1,1.5f),new Node("leather",2,1.5f),
                    new Node("leather_hat",3,0),new Node("leather_vest",3,1),
                    new Node("leather_pants",3,2),new Node("leather_shoes",3,3),
                    new Node("slime",0,4),new Node("adhesive",1,4),
                    new Node("obsidian",0,5.5f),new Node("obsidian_arrowhead",1,5.5f),new Node("obsidian_arrow",2,5.5f) };
            }
        }
    }
}
