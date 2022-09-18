using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SMLHelper.V2.Commands;
using UnityEngine;
using Logger = QModManager.Utility.Logger;

namespace Wander
{
    

    public static class Wander
    {
        public static bool DeveloperMode = true;
        
        public static Player Player;

        public static IDictionary<string, Path> Vagabonds = new Dictionary<string, Path>();

        //The global countdowns for updating stops and refreshing leash reminders
        public static float LastUpdated = 0;
        public static float LastLeashed = 0;
        
        public class Path
        {
            public Creature Creature;
            public Beacon Beacon;

            public SortedDictionary<float, Vector3> Stops;
            public float LastStopReached; //This time scalar value for the Creatures last reached stop
            public float CurrentStop;
            
            public Path(TechType creatureType, Vector3 start)
            {
                //Initialize our stop information
                Stops = new SortedDictionary<float, Vector3>();
                Stops[0] = start;
                LastStopReached = 0;

                //Create our Creature
                var creatureObj = CraftData.GetPrefabForTechType(creatureType);
                LiveMixin mixin = creatureObj.GetComponent<LiveMixin>();
                mixin = UnityEngine.Object.Instantiate(mixin, start, new Quaternion());

                Logger.Log(Logger.Level.Debug, "Created creature" + mixin);
                
                //Fetch our Creature
                Creature = mixin.GetComponent<Creature>();
                
                Logger.Log(Logger.Level.Debug, "Loaded creature" + Creature);
                
                //Leash our Creature to their spawn location to start
                Creature.leashPosition = start;

                if (DeveloperMode)
                {
                    var beaconObj = CraftData.GetPrefabForTechType(TechType.Beacon);
                    
                    Logger.Log(Logger.Level.Debug, "got beacon obj!" + beaconObj);
                    
                    beaconObj = UnityEngine.Object.Instantiate(beaconObj, start, new Quaternion());
Logger.Log(Logger.Level.Debug, "created beacon!" + beaconObj);
                    Beacon = beaconObj.GetComponent<Beacon>();
                    Beacon.label = "CreatureInitStop";
                }
            }
        }

        public static void CreateVagabonds()
        {
            //is eclipse if lightscalar dips below 5 between dayscalar of .15 and .85 
            if (Vagabonds.Count > 0)
                return;
            
            //Spawn in our Koosh Reaper just above the hole on the border between the Koosh Zone and the Mountains
            Vagabonds["KooshReaper"] = new Path(TechType.ReaperLeviathan, new Vector3(1173, -319, 902));
            Vagabonds["KooshReaper"].Stops[0.3f] = new Vector3(1219, -213, 866);
            Vagabonds["KooshReaper"].Stops[0.6f] = new Vector3(882, -157, 412);
            Vagabonds["KooshReaper"].Stops[0.9f] = new Vector3(428, -96, 171);
            
            Logger.Log(Logger.Level.Error, "Created KooshReaper Vagabond!" + Vagabonds["KooshReaper"]);
        }
        
        public static void UpdatePaths()
            {
                if (Vagabonds.Count == 0)
                {
                    Logger.Log(Logger.Level.Debug, "Attempting to load in Vagabonds...");
                    CreateVagabonds();
                    return;
                }

                foreach(KeyValuePair<string, Path> vagabond in Vagabonds)
                {
                    float[] stopTimes = vagabond.Value.Stops.Keys.ToArray();
                    
                    //Get the stop that's scheduled closest to the current time of day
                    var desiredStop = 0f;
                    var nextStop = 0f;
                    int index;
                    for (index = 0; index < stopTimes.Length; index++)
                    {
                        if (stopTimes[index] == vagabond.Value.LastStopReached)
                        {
                            if (index + 1 < stopTimes.Length)
                                nextStop = stopTimes[index + 1];
                            else
                                nextStop = 0f;
                        }


                        if (stopTimes[index] >= Time.OfDay && desiredStop == 0f)
                            desiredStop = stopTimes[index];
                    }

                    Logger.Log(Logger.Level.Debug, "Index: " + index);
                    if (desiredStop == 0f)
                        desiredStop = stopTimes[index - 1];
                    

                    //If the desired stop is further than the next available stop, have the creature go to the next available stop instead.
                    if (desiredStop > nextStop)
                        vagabond.Value.CurrentStop = nextStop;
                    else
                        vagabond.Value.CurrentStop = desiredStop;
                    
                    Logger.Log(Logger.Level.Debug, "Time: " + Time.OfDay + " Desired: " + desiredStop + " Next: " + nextStop + " Chosen: " + vagabond.Value.CurrentStop);
                    
                    var stop = vagabond.Value.Stops[vagabond.Value.CurrentStop];

                    Logger.Log(Logger.Level.Debug, "Key: " + vagabond.Key + " Creature: " + vagabond.Value.Creature);

                    
                    var distanceFromStop = Vector3.Distance(stop, vagabond.Value.Creature.transform.position);
                    
                    Logger.Log(Logger.Level.Debug, "Vagabond " + vagabond.Key + " is " + distanceFromStop.ToString() + " from stop " + stop.ToString());
                    if (distanceFromStop < 80)
                    {
                        Logger.Log(Logger.Level.Debug, "Vagabond " + vagabond.Key + " progressed from stop " + vagabond.Value.CurrentStop);
                        vagabond.Value.LastStopReached = vagabond.Value.CurrentStop;
                    }
                }
            }
    }
    
    //This is where we'll initialize everything
    [HarmonyPatch(typeof(Player))]
    [HarmonyPatch("Awake")]
    public class Init
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            Wander.Player = __instance;

            Wander.UpdatePaths();
        }
    }

    //Handle all our time/light related details
    [HarmonyPatch(typeof(DayNightCycle))]
    [HarmonyPatch("Update")]
    public class Time
    {
        public static float OfDay;
        private static float _lightScalar;
        
        [HarmonyPostfix]
        public static void Postfix(DayNightCycle __instance)
        {
            
            _lightScalar = __instance.GetLightScalar();            
            OfDay = __instance.GetDayScalar();
            
            if (Wander.LastUpdated > OfDay || OfDay - Wander.LastUpdated > .01)
            {
                Wander.LastUpdated = OfDay;
                Wander.UpdatePaths();
            } 
            else if (OfDay - Wander.LastLeashed > .01)
            {
                foreach (KeyValuePair<string, Wander.Path> vagabond in Wander.Vagabonds)
                {
                    if (vagabond.Value.LastStopReached == vagabond.Value.CurrentStop)
                        continue;

                    var lastAction = vagabond.Value.Creature.GetLastAction();
                    Logger.Log(Logger.Level.Debug, vagabond.Key + " did " + lastAction.name + ", importance: " + lastAction.evaluatePriority);

                    Wander.LastLeashed = OfDay;
                    vagabond.Value.Creature.leashPosition = vagabond.Value.Stops[vagabond.Value.CurrentStop];

                    if (Wander.DeveloperMode)
                    {
                        Logger.Log(Logger.Level.Debug, "Moving beacon " + vagabond.Value.Beacon);
                        vagabond.Value.Beacon.transform.position = vagabond.Value.Stops[vagabond.Value.CurrentStop];
                        vagabond.Value.Beacon.label = vagabond.Key + " Leash Pos";
                    }
                }
            }
        }
        
        //is eclipse if lightscalar dips below 5 between dayscalar of .15 and .85 
        public bool IsEclipse()
        {
            return (_lightScalar < 5 && (OfDay > .15 && OfDay < .85));
        }
    }

    public class Commands
    {
        [ConsoleCommand("wander")]
        public static string WanderCmd(string creatureName = "", string secondary = "")
        {
            if (creatureName.Length  > 0 && Wander.Vagabonds.ContainsKey(creatureName))
            {
                Logger.Log(Logger.Level.Debug, "Found creature: " + Wander.Vagabonds[creatureName].Creature);
                
                if (secondary.Length > 0 && secondary == "spy")
                {
                    return $"" + creatureName + " is currently " + Wander.Vagabonds[creatureName].Creature;
                }

                Wander.Player.transform.position = Wander.Vagabonds[creatureName].Creature.transform.position;
                return $"Sending to " + creatureName;
            }
            
            Wander.UpdatePaths();
            
            Logger.Log(Logger.Level.Debug, "Received Wander cmd!");
            return $"Parameters: {creatureName} {secondary}";
        }
    }
}