// Code from : https://github.com/0xDrMoe/TownofHost-Enhanced/blob/15be5ef66a3e08c6c8d53e2d5771ab5a4d51da7e/Modules/DelayNetworkedData.cs
using Hazel;
using InnerNet;
using System;
using UnityEngine;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;

namespace MoreGamemodes;

[HarmonyPatch(typeof(InnerNetClient))]
public class InnerNetClientPatch
{
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendInitialData))]
    [HarmonyPrefix]
    public static bool SendInitialDataPrefix(InnerNetClient __instance, int clientId)
    {
        if (!Main.ModdedProtocol.Value || __instance.NetworkMode != NetworkModes.OnlineGame) return true;
        // We make sure other stuffs like playercontrol and Lobby behavior is spawned properly
        // Then we spawn networked data for new clients
        MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
        messageWriter.StartMessage(6);
        messageWriter.Write(__instance.GameId);
        messageWriter.WritePacked(clientId);
        Il2CppSystem.Collections.Generic.List<InnerNetObject> obj = __instance.allObjects;
        lock (obj)
        {
            HashSet<GameObject> hashSet = new();
            for (int i = 0; i < __instance.allObjects.Count; i++)
            {
                InnerNetObject innerNetObject = __instance.allObjects[i];
                if (innerNetObject && (innerNetObject.OwnerId != -4 || __instance.AmModdedHost) && hashSet.Add(innerNetObject.gameObject))
                {
                    GameManager gameManager = innerNetObject as GameManager;
                    if (gameManager != null)
                    {
                        __instance.SendGameManager(clientId, gameManager);
                    }
                    else
                    {
                        if (innerNetObject is not NetworkedPlayerInfo)
                            __instance.WriteSpawnMessage(innerNetObject, innerNetObject.OwnerId, innerNetObject.SpawnFlags, messageWriter);
                    }
                }
            }
            messageWriter.EndMessage();
            __instance.SendOrDisconnect(messageWriter);
            messageWriter.Recycle();
        }
        DelaySpawnPlayerInfo(__instance, clientId);
        return false;
    }

    private static void DelaySpawnPlayerInfo(InnerNetClient __instance, int clientId)
    {
        if (!Main.ModdedProtocol.Value) return;
        List<NetworkedPlayerInfo> players = GameData.Instance.AllPlayers.ToArray().ToList();
        //Logging Stuff
        Main.Instance.Log.LogInfo(players);

        // We send 5 players at a time to prevent too huge packet
        while (players.Count > 0)
        {
            var batch = players.Take(5).ToList();

            MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
            messageWriter.StartMessage(6);
            messageWriter.Write(__instance.GameId);
            messageWriter.WritePacked(clientId);

            foreach (var player in batch)
            {
                if (player !=  null && player.ClientId != clientId && !player.Disconnected)
                {
                    __instance.WriteSpawnMessage(player, player.OwnerId, player.SpawnFlags, messageWriter);
                }
                players.Remove(player);
            }
            messageWriter.EndMessage();
            __instance.SendOrDisconnect(messageWriter);
            messageWriter.Recycle();
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
    [HarmonyPrefix]
    public static bool SendAllStreamedObjectsPrefix(InnerNetClient __instance, ref bool __result)
    {
        if (!Main.ModdedProtocol.Value || __instance.NetworkMode != NetworkModes.OnlineGame) return true;
        // Bypass all NetworkedData here.
        __result = false;
        Il2CppSystem.Collections.Generic.List<InnerNetObject> obj = __instance.allObjects;
        lock (obj)
        {
            for (int i = 0; i < __instance.allObjects.Count; i++)
            {
                InnerNetObject innerNetObject = __instance.allObjects[i];
                if (innerNetObject && innerNetObject is not NetworkedPlayerInfo && innerNetObject.IsDirty && (innerNetObject.AmOwner || (innerNetObject.OwnerId == -2 && __instance.AmHost)))
                {
                    MessageWriter messageWriter = __instance.Streams[(int)innerNetObject.sendMode];
                    messageWriter.StartMessage(1);
                    messageWriter.WritePacked(innerNetObject.NetId);
                    try
                    {
                        if (innerNetObject.Serialize(messageWriter, false))
                        {
                            messageWriter.EndMessage();
                        }
                        else
                        {
                            messageWriter.CancelMessage();
                        }
                        if (innerNetObject.Chunked && innerNetObject.IsDirty)
                        {
                            __result = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.Instance.Log.LogError(ex + "SendAllStreamedObjectsPrefix");
                        messageWriter.CancelMessage();
                    }
                }
            }
        }
        for (int j = 0; j < __instance.Streams.Length; j++)
        {
            MessageWriter messageWriter2 = __instance.Streams[j];
            if (messageWriter2.HasBytes(7))
            {
                messageWriter2.EndMessage();
                __instance.SendOrDisconnect(messageWriter2);
                messageWriter2.Clear((SendOption)j);
                messageWriter2.StartMessage(5);
                messageWriter2.Write(__instance.GameId);
            }
        }
        return false;
    }

    private static byte timer = 0;
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
    [HarmonyPostfix]
    public static void FixedUpdatePostfix(InnerNetClient __instance)
    {
        // Send a networked data pre 2 fixed update should be a good practice?
        if (!__instance.AmHost || __instance.Streams == null || __instance.NetworkMode != NetworkModes.OnlineGame || !Main.ModdedProtocol.Value) return;

        if (timer == 0)
        {
            timer = 1;
            return;
        }

        var player = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(x => x.IsDirty);
        if (player != null)
        {
            timer = 0;
            MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
            messageWriter.StartMessage(5);
            messageWriter.Write(__instance.GameId);
            messageWriter.StartMessage(1);
            messageWriter.WritePacked(player.NetId);
            try
            {
                if (player.Serialize(messageWriter, false))
                {
                    messageWriter.EndMessage();
                }
                else
                {
                    messageWriter.CancelMessage();
                    player.ClearDirtyBits();
                    return;
                }
                messageWriter.EndMessage();
                __instance.SendOrDisconnect(messageWriter);
                messageWriter.Recycle();
            }
            catch (Exception ex)
            {
                Main.Instance.Log.LogError(ex + "FixedUpdatePostfix");
                messageWriter.CancelMessage();
                player.ClearDirtyBits();
            }
        }
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.DirtyAllData))]
internal class DirtyAllDataPatch
{
    // Currently this function only occurs in CreatePlayer
    // It's believed to lag host, delay the playercontrol spawn mesasge & blackout new client
    // & send huge packets to all clients & completely no need to run
    // Temporarily disable it until Innersloth get a better fix.
    public static bool Prefix()
    {
        return false;
    }
}