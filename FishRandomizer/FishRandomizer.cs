using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Logger = QModManager.Utility.Logger;

namespace FishRandomizer
{
    public class FishRandomizer
    {
        public static List<string> Creatures = new List<string>();
        
        [HarmonyPatch(typeof(Player))]
        [HarmonyPatch("Awake")]
        public class PlayerPatch
        {
            public static Player Player;
        
            [HarmonyPostfix]
            public static void Postfix()
            {
                //Vanilla Creatures
                // Creatures.Add("LavaLarva");
                // Creatures.Add("Eyeye");
                // Creatures.Add("Garryfish");
                // Creatures.Add("Holefish");
                // Creatures.Add("JellyRay");
                // Creatures.Add("Peeper");
                // Creatures.Add("RabbitRay");
                // Creatures.Add("Reefback");
                // Creatures.Add("Reginald");
                // Creatures.Add("SandShark");
                // Creatures.Add("Stalker");
                // Creatures.Add("BladderFish");
                // Creatures.Add("Mesmer");
                // Creatures.Add("Bleeder");
                // Creatures.Add("BoneShark");
                // Creatures.Add("CuteFish");
                // Creatures.Add("ReaperLeviathan");
                // Creatures.Add("CaveCrawler");
                // Creatures.Add("Biter");
                // Creatures.Add("Shocker");
                // Creatures.Add("CrabSnake");
                // Creatures.Add("SpineEel");
                // Creatures.Add("SeaTreader");
                // Creatures.Add("CrabSquid");
                // Creatures.Add("Warper");
                // Creatures.Add("LavaLizard");
                // Creatures.Add("SeaDragon");
                // Creatures.Add("SeaEmperorBaby");
                // Creatures.Add("GhostLeviathan");
                // Creatures.Add("SeaEmperorJuvenile");
                
                if (QModManager.API.QModServices.Main.ModPresent("DeExtinction"))
                {
                    Logger.Log(Logger.Level.Debug, "DeExtinction Detected, Loading Creatures");
                    
                    //De-Extinction Creatures
                    Creatures.Add("StellarThalassacean");
                    Creatures.Add("JasperThalassacean");
                    Creatures.Add("GrandGlider");
                    Creatures.Add("GulperLeviathan");
                    Creatures.Add("Axetail");
                    Creatures.Add("RibbonRay");
                    Creatures.Add("Twisteel");
                    Creatures.Add("Filtorb");
                    Creatures.Add("JellySpinner");
                    Creatures.Add("TriangleFish");
                }
            }
        }
        
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
                                var rand = random.Next(0, Creatures.Count);

                                var creature = Creatures[rand];

                                if (creature == "SandShark")
                                    creature = "Sandshark";
                                
                                if (creature == "Garryfish")
                                    creature = "GarryFish";
                                
                                if (creature == "Holefish")
                                    creature = "HoleFish";

                                if (creature == "CrabSnake")
                                    creature = "Crabsnake";
                                
                                if (creature == "BladderFish")
                                    creature = "Bladderfish";
                                
                                if (creature == "JellyRay")
                                    creature = "Jellyray";

                                //De-Extinction checks
                                
                                if (creature == "TriangleFish")
                                    creature = "Trianglefish";

                                Enum.TryParse(creature, out TechType type);

                                if (type == TechType.None)
                                {
                                    Logger.Log(Logger.Level.Debug, "Couldn't fetch " + creature + " as random creature!");
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