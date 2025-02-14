﻿using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ScrambledSeas
{
    public static class Patches
    {
        [HarmonyPatch(typeof(IslandHorizon), "Start")]
        private static class IslandPatch
        {
            private static void Prefix(IslandHorizon __instance)
            {
                if (Main.pluginEnabled && __instance.islandIndex > 0)
                {
                    WorldScrambler.islandNames[__instance.islandIndex - 1] = __instance.gameObject.name;
                    WorldScrambler.islandOrigins[__instance.islandIndex - 1] = __instance.gameObject.transform.localPosition;
                }
            }
        }

        [HarmonyPatch(typeof(SaveLoadManager), "SaveModData")]
        private static class SavePatch
        {
            private static void Postfix()
            {
                if (Main.pluginEnabled)
                {
                    Main.saveContainer.version = WorldScrambler.version;
                    SaveFileHelper.Save(Main.saveContainer, "ScrambledSeas");
                }
            }
        }

        [HarmonyPatch(typeof(SaveLoadManager), "LoadModData")]
        private static class LoadGamePatch
        {
            private static void Postfix()
            {
                if (Main.pluginEnabled)
                {
                    //Load entire ScrambledSeasSaveContainer from save file
                    Main.saveContainer = SaveFileHelper.Load<ScrambledSeasSaveContainer>("ScrambledSeas");

                    if (Main.saveContainer.version < 60)
                    { //TODO: update min version if save compatibility breaks again
                        NotificationUi.instance.ShowNotification("ERROR: This save is not\ncompatiblewith this version\nof Scrambled Seas");
                        throw new System.InvalidOperationException("ERROR: This save is not compatible with this version of Scrambled Seas");
                    }
                    //Re-generate world for the saved randomizer params
                    WorldScrambler.Scramble();
                }
            }
        }

        [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
        private static class StartNewGamePatch
        {
            private static bool Prefix(StartMenu __instance, ref bool ___fPressed, ref Transform ___playerObserver, ref GameObject ___playerController, ref int ___animsPlaying, ref int ___currentRegion, ref Transform ___startApos, ref Transform ___startEpos, ref Transform ___startMpos)
            {
                if (Main.pluginEnabled)
                {
                    //Create a randomized world with a new seed
                    Main.saveContainer.worldScramblerSeed = (int)System.DateTime.Now.Ticks;
                    WorldScrambler.Scramble();
                    //Move player start positions to new island locations
                    ___startApos.position += WorldScrambler.islandDisplacements[2];
                    ___startEpos.position += WorldScrambler.islandDisplacements[10];
                    ___startMpos.position += WorldScrambler.islandDisplacements[20];

                    ___animsPlaying++;
                    Transform transform = null;
                    if (___currentRegion == 0)
                    {
                        transform = ___startApos;
                        GameState.newGameRegion = PortRegion.alankh;
                    }
                    else if (___currentRegion == 1)
                    {
                        transform = ___startEpos;
                        GameState.newGameRegion = PortRegion.emerald;
                    }
                    else
                    {
                        transform = ___startMpos;
                        GameState.newGameRegion = PortRegion.medi;
                    }

                    __instance.InvokePrivateMethod("DisableIslandMenu");
                    __instance.StartCoroutine(MovePlayerToStartPos(__instance, transform, ___playerObserver, ___playerController));

                    return false;
                }
                return true;
            }

            public static IEnumerator MovePlayerToStartPos(StartMenu instance, Transform startPos, Transform playerObserver, GameObject playerController)
            {
                playerObserver.transform.parent = instance.gameObject.transform.parent;
                playerObserver.position = startPos.position;
                playerController.transform.position = startPos.position;
                instance.GetPrivateField<GameObject>("logo").SetActive(false);
                instance.GetPrivateField<Transform>("playerObserver").transform.parent = instance.transform.parent;
                float animTime = 0;
                Juicebox.juice.TweenPosition(instance.GetPrivateField<Transform>("playerObserver").gameObject, startPos.position, animTime, JuiceboxTween.quadraticInOut);
                for (float t = 0f; t < animTime; t += Time.deltaTime)
                {
                    instance.GetPrivateField<Transform>("playerObserver").rotation = Quaternion.Lerp(instance.GetPrivateField<Transform>("playerObserver").rotation, startPos.rotation, Time.deltaTime * 0.35f);
                    yield return new WaitForEndOfFrame();
                }
                instance.GetPrivateField<Transform>("playerObserver").rotation = startPos.rotation;
                instance.GetPrivateField<GameObject>("playerController").transform.position = instance.GetPrivateField<Transform>("playerObserver").position;
                instance.GetPrivateField<GameObject>("playerController").transform.rotation = instance.GetPrivateField<Transform>("playerObserver").rotation;
                yield return new WaitForEndOfFrame();
                instance.GetPrivateField<GameObject>("playerController").GetComponent<CharacterController>().enabled = true;
                instance.GetPrivateField<GameObject>("playerController").GetComponent<OVRPlayerController>().enabled = true;
                instance.GetPrivateField<Transform>("playerObserver").gameObject.GetComponent<PlayerControllerMirror>().enabled = true;
                MouseLook.ToggleMouseLookAndCursor(true);
                instance.GetPrivateField<PurchasableBoat[]>("startingBoats")[instance.GetPrivateField<int>("currentRegion")].LoadAsPurchased();
                instance.StartCoroutine(Blackout.FadeTo(1f, 0.2f));
                yield return new WaitForSeconds(0.2f);
                yield return new WaitForEndOfFrame();
                instance.GetPrivateField<GameObject>("disclaimer").SetActive(true);
                instance.SetPrivateField("waitingForFInput", true);
                while (!instance.GetPrivateField<bool>("fPressed"))
                {
                    yield return new WaitForEndOfFrame();
                }
                instance.GetPrivateField<GameObject>("disclaimer").SetActive(false);
                instance.StartCoroutine(Blackout.FadeTo(0f, 0.3f));
                yield return new WaitForEndOfFrame();
                SaveLoadManager.readyToSave = true;
                GameState.playing = true;
                GameState.justStarted = true;
                MouseLook.ToggleMouseLook(true);
                int animsPlaying = (int)Traverse.Create(instance).Field("animsPlaying").GetValue();
                Traverse.Create(instance).Field("animsPlaying").SetValue(animsPlaying - 1);
                yield return new WaitForSeconds(1f);
                GameState.justStarted = false;
                yield break;
            }
        }

        [HarmonyPatch(typeof(StartMenu), "MovePlayerToStartPos")]
        private static class MovePlayerPatch
        {
            private static void Prefix(Transform startPos, StartMenu __instance, ref Transform ___playerObserver, ref GameObject ___playerController)
            {
                if (Main.pluginEnabled)
                {
                    //Teleport player to shifted starting position
                    ___playerObserver.transform.parent = __instance.gameObject.transform.parent;
                    ___playerObserver.position = startPos.position;
                    ___playerController.transform.position = startPos.position;
                    //This will be followed by an animation performed by Juicebox.juice.TweenPosition(), but it messes the position up. I've disabled it below...
                }
            }
        }

        [HarmonyPatch(typeof(Juicebox), "TweenPosition", new System.Type[] { typeof(GameObject), typeof(Vector3), typeof(float), typeof(JuiceboxTween) })]
        private static class TweenPatch
        {
            private static bool Prefix()
            {
                if (Main.pluginEnabled)
                {
                    //This just disables the original method completely. Otherwise, it glitches out while moving the player over a large distance.
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(StarterSet), "InitiateStarterSet")]
        private static class StarterSetPatch
        {
            private static void Prefix(StarterSet __instance)
            {
                if (Main.pluginEnabled)
                {
                    //Figure out which island we start on
                    Vector3 startOffset = new Vector3();
                    if (__instance.region == PortRegion.alankh)
                    {
                        startOffset = WorldScrambler.islandDisplacements[2];
                        PlayerGold.currency[0] += 48;
                    }
                    if (__instance.region == PortRegion.emerald)
                    {
                        startOffset = WorldScrambler.islandDisplacements[10];
                        PlayerGold.currency[1] += 48;
                    }
                    if (__instance.region == PortRegion.medi)
                    {
                        startOffset = WorldScrambler.islandDisplacements[20];
                        PlayerGold.currency[2] += 48;
                    }
                    //Move starter set items to new island location
                    GameObject mapObject = null;
                    foreach (Transform starterItem in __instance.gameObject.transform)
                    {
                        starterItem.Translate(startOffset, Space.World);
                        if (starterItem.name.ToLower().Contains("map"))
                        {
                            mapObject = starterItem.gameObject;
                        }
                    }
                    if (mapObject)
                    {
                        GameObject.Destroy(mapObject);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerReputation), "GetMaxDistance")]
        private static class ReputationPatch
        {
            private static void Postfix(ref float __result)
            {
                //Islands tend to be farther apart in this mod. Ensure that the returned value is at least 300 miles
                if (Main.pluginEnabled && __result < 300f)
                {
                    __result = 300f;
                }
            }
        }

        [HarmonyPatch(typeof(MissionDetailsUI), "UpdateMap")]
        private static class MissionMapPatch
        {
            private static void Postfix(ref Renderer ___mapRenderer, ref TextMesh ___locationText)
            {
                if (Main.pluginEnabled)
                {
                    ___mapRenderer.gameObject.SetActive(false);
                    ___locationText.text = "Map Unavailable\n\nWelcome to ScrambledSeas :)";
                }
            }
        }

        [HarmonyPatch(typeof(RegionBlender), "Update")]
        private static class RegionBlenderPatch
        {
            private static float regionUpdateCooldown = 0f;

            private static void Prefix(ref Region ___currentTargetRegion, ref Transform ___player)
            {
                if (Main.pluginEnabled)
                {
                    if (regionUpdateCooldown <= 0f)
                    {
                        regionUpdateCooldown = 100f;
                        float minDist = 100000000f;
                        Region closestRegion = null;
                        foreach (Region region in WorldScrambler.regions)
                        {
                            float dist = Vector3.Distance(___player.position, region.transform.position);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestRegion = region;
                            }
                        }
                        if (closestRegion != null)
                        {
                            ___currentTargetRegion = closestRegion;
                        }
                    }
                    else
                    {
                        regionUpdateCooldown -= Time.deltaTime;
                    }
                }
            }
        }
    }
}
