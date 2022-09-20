using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using SMLHelper.V2.Commands;
using Logger = QModManager.Utility.Logger;

//Diel Vertical Migration, The Mod
namespace Migrate
{
    public enum FoodChainStatus
    {
        Predator,   //Run
        Prey,       //Hunt
        None        //This is for creatures outside the normal foodchain, for example Reefback Leviathans
    };

    public enum MigrationTypes {
        //In all cases predators follow the prey
        DielNocturnal,  // - Prey try to maintain constant DARKNESS throughout the day/night
        DielReverse,    // - Prey try to maintain constant LIGHT throughout the day/night
        DielTwilight,   // - Prey go to the surface for sunrise and sunset and sink back down shortly after
        Ontogenetic     // - Creatures relocate through the water column as they live their lives (Not currently implemented)
    };
    
    //Our constructor
    public static class Helpers
    {
        /*
         * Creatures that migrate
         */
        public static IDictionary<string, Migrator> Migrators = new Dictionary<string, Migrator>();
        
        /*
         * The vertical paths followed by migrating Creatures
         */
        public static IDictionary<MigrationTypes, SortedList<double, double>> Traversals = new Dictionary<MigrationTypes, SortedList<double, double>>();

        /*
         * MigratorSizeExtremes = {smallest, largest} 
         * -Initialize these with values between the known smallest and largest creature and they'll be automatically worked out
         */
        public static double SmallestMigratorSize = 50; 
        public static double LargestMigratorSize = 50; 
        
        /*
         * Margins = {top, bottom}
         * The amount of space preserved at the top and bottom of the water column to which creatures will not be sent by this mod
         */
        public static int[] Margins = { 10, 10 };
        
        //How far should we try to move the creature up and down the water column per attempt
        public static int MigrationAmount = 30;

        //This is the depth our creature will assume the water column is if the bottom is unloaded and therefore unable to be found
        public static int FalseBottom = 300;
        
        
        static Helpers()
        {
            //Initialize our traversal paths
            InitTraversals();

            //Register Nocturnal Prey
            Migrators.Add(GetCreatureName(TechType.Peeper), new Migrator(TechType.Peeper, MigrationTypes.DielNocturnal, FoodChainStatus.Prey, 0.75));
            Migrators.Add(GetCreatureName(TechType.Oculus), new Migrator(TechType.Oculus, MigrationTypes.DielNocturnal, FoodChainStatus.Prey, 0.75));
            Migrators.Add(GetCreatureName(TechType.Bladderfish), new Migrator(TechType.Bladderfish, MigrationTypes.DielNocturnal, FoodChainStatus.Prey, 0.75));
            Migrators.Add(GetCreatureName(TechType.Eyeye), new Migrator(TechType.Eyeye, MigrationTypes.DielNocturnal, FoodChainStatus.Prey, 0.75));
            Migrators.Add(GetCreatureName(TechType.Spadefish), new Migrator(TechType.Spadefish, MigrationTypes.DielNocturnal, FoodChainStatus.Prey, 0.75));
            
            //Register Diurnal Prey
            Migrators.Add(GetCreatureName(TechType.GarryFish), new Migrator(TechType.GarryFish, MigrationTypes.DielReverse, FoodChainStatus.Prey, 1));
            Migrators.Add(GetCreatureName(TechType.HoleFish), new Migrator(TechType.HoleFish, MigrationTypes.DielReverse, FoodChainStatus.Prey, 0.75));
            Migrators.Add(GetCreatureName(TechType.Reginald), new Migrator(TechType.Reginald, MigrationTypes.DielReverse, FoodChainStatus.Prey, 0.8));
            
            //Register Twilight Prey
            Migrators.Add(GetCreatureName(TechType.Mesmer), new Migrator(TechType.Mesmer, MigrationTypes.DielReverse, FoodChainStatus.Prey, 1.5));
            Migrators.Add(GetCreatureName(TechType.Boomerang), new Migrator(TechType.Boomerang, MigrationTypes.DielNocturnal, FoodChainStatus.Prey, 0.75));
            
            //Register Nocturnal Predators
            Migrators.Add(GetCreatureName(TechType.GhostLeviathan), new Migrator(TechType.GhostLeviathan, MigrationTypes.DielNocturnal, FoodChainStatus.Predator, 70)); // These are just so big that I actually sized them down from 107 in order to make this work better
            Migrators.Add(GetCreatureName(TechType.ReaperLeviathan), new Migrator(TechType.ReaperLeviathan, MigrationTypes.DielNocturnal, FoodChainStatus.Predator, 55));
            Migrators.Add(GetCreatureName(TechType.Shocker), new Migrator(TechType.Shocker, MigrationTypes.DielNocturnal, FoodChainStatus.Predator, 20));
            Migrators.Add(GetCreatureName(TechType.Biter), new Migrator(TechType.Biter, MigrationTypes.DielNocturnal, FoodChainStatus.Predator, 1));
            
            //Register Twilight Predators
            Migrators.Add(GetCreatureName(TechType.Sandshark), new Migrator(TechType.Sandshark, MigrationTypes.DielTwilight, FoodChainStatus.Predator, 2));
            
            //Some creatures have some unique locations and behaviors that make me reluctant to mess with them
            // Migrators.Add(GetCreatureName(TechType.BoneShark), new Migrator(TechType.BoneShark, MigrationTypes.DielNocturnal, FoodChainStatus.Predator, 18));
            // Migrators.Add(GetCreatureName(TechType.Stalker), new Migrator(TechType.Stalker, MigrationTypes.DielTwilight, FoodChainStatus.Predator, 4));
            
            //Register Creatures outside the regular food chain
            Migrators.Add(GetCreatureName(TechType.Reefback), new Migrator(TechType.Reefback, MigrationTypes.DielReverse, FoodChainStatus.None, 70));
            
            //This records our biggest and smallest creature sizes for reference
            SetMigratorSizeExtremes();
            
            //This records our creature sizes relative to the biggest registered above
            SetMigratorSizePlacements();
        }
        
        
        /*
         * Returns the maximum possible depth for a given entity vector
         */
        public static float GetPossibleEntityDepth(Vector3 entityPos)
        {
            RaycastHit hit;
    
            float entityToTerrain = Helpers.FalseBottom;

            if (Physics.Raycast(new Ray(entityPos, -Vector3.up), out hit))
                entityToTerrain = hit.distance;

            return Math.Abs(entityPos.y) + entityToTerrain;
        }
    
        /*
         * TechTypes are easier to keep track of but we still need the actual name given to Creatures sometimes
         */
        public static string GetCreatureName(TechType type)
        {
            string ret = "";
    
            var prefab = CraftData.GetPrefabForTechType(type);
            var newCreature = prefab.GetComponent<Creature>();

            if (newCreature != null)
                ret = newCreature.name;

            return ret;
        }
        
        /*
         * Initialize our migratory paths
         * Traversals[Type] = Vector2(lightScalar, depth% (0-1.0)
         */
        private static void InitTraversals()
        {
            foreach (MigrationTypes type in (MigrationTypes[])Enum.GetValues(typeof(MigrationTypes)))
                Traversals.Add(type, new SortedList<double, double>());
            
            //Diel Nocturnal Migration
            Traversals[MigrationTypes.DielNocturnal].Add(0, 0);
            Traversals[MigrationTypes.DielNocturnal].Add(0.2, 0.2);
            Traversals[MigrationTypes.DielNocturnal].Add(0.4, 0.4);
            Traversals[MigrationTypes.DielNocturnal].Add(0.6, 0.6);
            Traversals[MigrationTypes.DielNocturnal].Add(0.8, 0.8);
            Traversals[MigrationTypes.DielNocturnal].Add(1, 1);
            
            //Diel Reverse Migration
            Traversals[MigrationTypes.DielReverse].Add(0, 1);
            Traversals[MigrationTypes.DielReverse].Add(0.1, 1);
            Traversals[MigrationTypes.DielReverse].Add(0.2, 0.92);
            Traversals[MigrationTypes.DielReverse].Add(0.3, 0.75);
            Traversals[MigrationTypes.DielReverse].Add(0.4, 0.6);
            Traversals[MigrationTypes.DielReverse].Add(0.5, 0.5);
            Traversals[MigrationTypes.DielReverse].Add(0.6, 0.4);
            Traversals[MigrationTypes.DielReverse].Add(0.7, 0.3);
            Traversals[MigrationTypes.DielReverse].Add(0.8, 0.2);
            Traversals[MigrationTypes.DielReverse].Add(0.9, 0.1);
            Traversals[MigrationTypes.DielReverse].Add(1, 0);
            
            //Diel Twilight Migration
            Traversals[MigrationTypes.DielTwilight].Add(0, 0);
            Traversals[MigrationTypes.DielTwilight].Add(0.1, 0.8);
            Traversals[MigrationTypes.DielTwilight].Add(0.2, 0.9);
            Traversals[MigrationTypes.DielTwilight].Add(0.3, 0.6);
            Traversals[MigrationTypes.DielTwilight].Add(0.4, 0);
            Traversals[MigrationTypes.DielTwilight].Add(0.5, 0);
            Traversals[MigrationTypes.DielTwilight].Add(0.6, 0);
            Traversals[MigrationTypes.DielTwilight].Add(0.7, 0.6);
            Traversals[MigrationTypes.DielTwilight].Add(0.8, 0.9);
            Traversals[MigrationTypes.DielTwilight].Add(0.9, 0.8);
            Traversals[MigrationTypes.DielTwilight].Add(1, 0);
        }

        /*
         * This records our biggest and smallest creature sizes for reference
         */
        private static void SetMigratorSizeExtremes()
        {
            foreach (KeyValuePair<string, Migrator> migrator in Migrators)
            {
                if (migrator.Value.TypicalSize < SmallestMigratorSize)
                    SmallestMigratorSize = migrator.Value.TypicalSize;

                if (migrator.Value.TypicalSize > LargestMigratorSize)
                    LargestMigratorSize = migrator.Value.TypicalSize;
            }
        }

        /*
         * This records our creature sizes relative to the biggest creature registered 
         */
        private static void SetMigratorSizePlacements()
        {
            foreach (KeyValuePair<string, Migrator> migrator in Migrators)
            {
                migrator.Value.SizePlacement = migrator.Value.TypicalSize / LargestMigratorSize * 100;
                
                Logger.Log(Logger.Level.Debug, migrator.Key + " received a size placement of " + migrator.Value.SizePlacement);
            }
        }
    }
    
    
    // Each migratory species should be initialized as a Migrator
    public class Migrator
    {
        /*
         * These are available right after game launch as presets for the species
         */
        public TechType TechType;
        public MigrationTypes MigrationType;
        public FoodChainStatus FoodChainStatus;
        public double TypicalSize; //The typical size in approximate Meters
        public double SizePlacement; //The size% of our creature relative to that of the largest creature
         
        
        /*
         * These are only available at the call from our Creature UpdateBehaviour patch and are instance specific
         */
        private Creature _creature;
        private float _possibleDepth;
        private int _currentLightIndex; //The current index of our traversal according to the current light level
        
        //_traversalRange = Dictionary{current, upper, lower}
        private Dictionary<string, double> _traversalRange;

        public Migrator(TechType creature, MigrationTypes type, FoodChainStatus status, double typicalSize)
        {
            TechType = creature;
            MigrationType = type;
            FoodChainStatus = status;
            TypicalSize = typicalSize;
        }
    
        /*
         * Called by our Creature.UpdateBehavior patch to send our creatures to their proper migratory zone 
         */
        public void Migrate(Creature instance)
        {
            _creature = instance;
            _possibleDepth = this.GetPossibleDepth();

            Logger.Log(Logger.Level.Debug, _creature.name + " has possible depth of " + _possibleDepth + " and is performing " + MigrationType);
            
            //Set the traversal range based on environmental factors
            SetTraversalRange();
            
            //Adjust the traversal range based on creature specific factors
            AdjustTraversalRangeForCreature();

            var minHeight = _traversalRange["current"] - _traversalRange["lower"];
            var maxHeight = _traversalRange["current"] + _traversalRange["upper"];
            
            var creatureHeight = _creature.transform.position.y;

            //If our creature is within its traversal range or is bigger than its traversal range then we don't need to do anything
            if (creatureHeight > minHeight && creatureHeight < maxHeight)
                return;

            // var orientation = _creature.transform.rotation * Vector3.forward;
            var nextLoc = _creature.leashPosition; //new Ray(_creature.transform.position, orientation).GetPoint(20);
            var distanceTravelled = _creature.transform.position - nextLoc;
            var avgDistance = Math.Abs((distanceTravelled.x + distanceTravelled.z) / 2);

            if (creatureHeight < minHeight)
                nextLoc.y += avgDistance;

            if (creatureHeight > maxHeight)
                nextLoc.y -= avgDistance;

            if (Math.Abs(nextLoc.y) < Helpers.Margins[0])
            {
                Logger.Log(Logger.Level.Debug, "NextLoc " + nextLoc.y + " breached our top margin. Setting to " + Helpers.Margins[0]);
                nextLoc.y = -Helpers.Margins[0];
            }

            if (GetDistanceFromBottom() < Helpers.Margins[1])
            {
                Logger.Log(Logger.Level.Debug, "NextLoc " + nextLoc.y + " breached our bottom margin. Setting to " + (_possibleDepth + Helpers.Margins[1]) + " DFB: " + GetDistanceFromBottom());
                nextLoc.y = _possibleDepth + Helpers.Margins[1];
            }

            _creature.leashPosition = nextLoc;
            
            Logger.Log(Logger.Level.Debug, _creature.name + " was sent from " + _creature.transform.position + " to " + nextLoc + " with an avg distance travelled of " + avgDistance);
        }

        /*
         * Returns the current migratory range of our creature based on environmental factors
         */
        private void SetTraversalRange()
        {
            var traversal = Helpers.Traversals[MigrationType];
            var range = new Dictionary<string, double>();
            
            var desiredLight = traversal.OrderBy(v => Math.Abs(v.Key - Time.LightScalar)).First();
            _currentLightIndex = traversal.IndexOfKey(desiredLight.Key);

            //To begin, lets set our minimum traversal range to our previous stops depth
            var min = 
                _currentLightIndex == 0 
                    ? traversal.Values[0] 
                    : traversal.Values[_currentLightIndex - 1];
            
            //...and our maximum traversal range to our next stops depth
            var max = 
                traversal.Keys.Count - 1 == _currentLightIndex
                    ? traversal.Values[_currentLightIndex]
                    : traversal.Values[_currentLightIndex + 1];

            //Get the amount of traversal room that should be available above and below our creature
            var upperDiff = Math.Abs(traversal.Values[_currentLightIndex] - min);
            var lowerDiff = Math.Abs(traversal.Values[_currentLightIndex] - max);

            //Set our traversal ranges
            range["current"] = (_possibleDepth * desiredLight.Value) * -1;
            range["upper"] = _possibleDepth * upperDiff;
            range["lower"] = _possibleDepth * lowerDiff;

            Logger.Log(Logger.Level.Debug, "The water column is " + _possibleDepth + " deep. Fish is @ " + _creature.transform.position.y + " and should be between " + (range["current"] - range["lower"]) + " & " + (range["current"] + range["upper"]) + " (Target: " + range["current"] + ")");

            _traversalRange = range;
        }

        /*
         * Returns the current migratory range of our creature based on creature specific factors
         */
        private void AdjustTraversalRangeForCreature()
        {
            var medianDepth = Helpers.Traversals[MigrationType].Values[_currentLightIndex];
            var inColumn =  medianDepth > 0 && medianDepth < 1;
            
            //If the creature is prey and not at its deepest traversal index then limit its lower range by size
            if (FoodChainStatus == FoodChainStatus.Prey && !inColumn)
                _traversalRange["lower"] = _traversalRange["lower"] / 100 * SizePlacement;
            
            //If the creature is a predator and not at its deepest traversal index then limit its upper range by size
            if (FoodChainStatus == FoodChainStatus.Predator && !inColumn)
                _traversalRange["upper"] -= _traversalRange["upper"] / 100 * SizePlacement;
        }

        private float GetDistanceFromBottom()
        {
            RaycastHit hit;
            
            float entityToTerrain = Helpers.FalseBottom;

            if (Physics.Raycast(new Ray(_creature.transform.position, -Vector3.up), out hit))
                entityToTerrain = hit.distance;

            return entityToTerrain;
        }

        /*
         * Return the maximum possible z axis depth for our terrain at the creatures x and z axis.
         */
        private float GetPossibleDepth()
        {
            return Helpers.GetPossibleEntityDepth(_creature.transform.position);
        }
    }
    
    [HarmonyPatch(typeof(Creature))]
    [HarmonyPatch("UpdateBehaviour")]
    public class PatchCreatureBehaviour
    {
        [HarmonyPostfix]
        public static void Postfix(Creature __instance)
        {
            if (__instance.GetBestAction() == null) 
                return;
                
            var action = __instance.GetBestAction().ToString().Split('(').Last().Replace(")", "");
            var name = __instance.name.Replace("(Clone)", "");

            string[] replaceableActions = { "SwimRandom" };
            string[] activeBiomes = { "kooshZone", "mountains", "grandReef", "seaTreaderPath", "dunes", "bloodKelp", "GrassyPlateaus", "SparseReef", "kelpForest" };
    
            //If our creature is a valid migrator and is performing a replaceable action and is in an active biome, process our creature for migration
            if (Helpers.Migrators.ContainsKey(name) && Array.IndexOf(replaceableActions, action) > -1 && Array.IndexOf(activeBiomes, WorldPatch.World.GetBiome(__instance.transform.position).ToString()) > -1)
                Helpers.Migrators[name].Migrate(__instance);
             
            if (Helpers.Migrators.ContainsKey(name))
                Logger.Log(Logger.Level.Debug, __instance + " is performing " + action);
        }
    }
    

    //Handle all our time/light related details
    [HarmonyPatch(typeof(DayNightCycle))] 
    [HarmonyPatch("Update")]
    public class Time
    {
        public static float OfDay;
        public static float LightScalar;
        
        [HarmonyPostfix]
        public static void Postfix(DayNightCycle __instance)
        {
            LightScalar = __instance.GetLocalLightScalar();            
            OfDay = __instance.GetDayScalar();
            
            // Logger.Log(Logger.Level.Debug, "Light: " + LightScalar + " | Local Light: " + LightScalar);
        }
    
        //is eclipse if lightscalar dips below 5 between dayscalar of .15 and .85 
        public bool IsEclipse()
        {
            return (LightScalar < 5 && (OfDay > .15 && OfDay < .85));
        }
    }


    [HarmonyPatch(typeof(LargeWorld))]
    [HarmonyPatch("Awake")]
    public class WorldPatch
    {
        public static LargeWorld World;
        
        [HarmonyPostfix]
        public static void Postfix(LargeWorld __instance)
        {
            World = __instance;
        }
    }
    
    [HarmonyPatch(typeof(Player))]
    [HarmonyPatch("Awake")]
    public class PlayerPatch
    {
        public static Player Player;
        
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            Player = __instance;
        }
    }
    
    
    public class Commands
    {
        
        [ConsoleCommand("GetBiome")]
        public static string MigrateCmd()
        {
            return $"Biome = " + WorldPatch.World.GetBiome(PlayerPatch.Player.transform.position);
        }
        
        [ConsoleCommand("playerSpeed")]
        public static string PlayerSpeedCmd(int speed = 1)
        {
            PlayerPatch.Player.movementSpeed = speed;
            
            return $"Parameters: {speed}";
        }
        
        [ConsoleCommand("margins")]
        public static string MarginsCmd()
        {
            return $"" + Helpers.Margins[0] + " | " + Helpers.Margins[1];
        }
    }
}