using System;
using HarmonyLib;
using UnityEngine;
using Logger = QModManager.Utility.Logger;

namespace FishRandomizer
{
    public class FishRandomizer
    {
        public static string[] Creatures =
        {
            "BloomCreature",
            "LavaLarva",
            "Eyeye",
            "Garryfish",
            "Holefish",
            "JellyRay",
            "Peeper",
            "RabbitRay",
            "Reefback",
            "Reginald",
            "SandShark",
            "Stalker",
            "BladderFish",
            "Mesmer",
            "Bleeder",
            "BoneShark",
            "CuteFish",
            "ReaperLeviathan",
            "CaveCrawler",
            "Biter",
            "Shocker",
            "CrabSnake",
            "SpineEel",
            "SeaTreader",
            "CrabSquid",
            "Warper",
            "LavaLizard",
            "SeaDragon",
            "SeaEmperorBaby",
            "GhostLeviathan",
            "SeaEmperorJuvenile",
            "GhostLeviatanVoid"
        };

        [HarmonyPatch(typeof(Knife))]
        [HarmonyPatch("OnToolUseAnim")]
        internal class PatchCreatureOnTakeDamage
        {
            [HarmonyPostfix]
            public static void Postfix(Knife __instance, GUIHand hand)
            {
                var position = default(Vector3);
                GameObject gameObject = null;
                GameObject targetObject = null;
                UWE.Utils.TraceFPSTargetPosition(Player.main.gameObject, __instance.attackDist, ref gameObject, ref position, true);
                
                
                if (gameObject)
                {
                    
                    LiveMixin liveMixin = gameObject.FindAncestor<LiveMixin>();

                    if (liveMixin)
                    {

                        targetObject = liveMixin.gameObject;
                        
                        var isAlive = liveMixin.IsAlive();
                        var className = targetObject.name.Replace("(Clone)", "");
                        var isCreature = (Creatures.IndexOf(className) > -1);
                        
                        Logger.Log(Logger.Level.Debug, "Class: " + className + " | IsCreature: " + isCreature.ToString() + " | IsAlive: " + isAlive);

                        if (isAlive && isCreature)
                        {
                            var pos = liveMixin.transform.position;
                            var rotation = liveMixin.transform.rotation;
                            var health = liveMixin.health;
                            var parent = liveMixin.transform.parent;
                            
                            Logger.Log(Logger.Level.Debug, "Randomizing " + className + "! | At Pos: " + pos.ToString() + " | Obj Pos: " + targetObject.transform.position + " | Health: " + health);
                            
                            UnityEngine.Object.Destroy(liveMixin.gameObject);

                            TechType getRandomCreature()
                            {
                                var random = new System.Random();
                                var rand = random.Next(0, Creatures.Length);

                                Enum.TryParse(Creatures[rand], out TechType type);

                                if (type == TechType.None)
                                {
                                    Logger.Log(Logger.Level.Debug, "Couldn't fetch " + Creatures[rand] + " as random creature!");
                                    type = getRandomCreature();
                                }

                                return type;
                            }

                            var creatureType = getRandomCreature();
                            
                            Logger.Log(Logger.Level.Debug, "Type Chosen: " + creatureType);

                            var newObject = CraftData.GetPrefabForTechType(creatureType);
                            var newCreature = newObject.GetComponent<LiveMixin>();
                            newCreature.health = health;

                            UnityEngine.Object.Instantiate(newCreature, pos, rotation, parent);
                        }
                    }
                }
            }
        }
    }
}