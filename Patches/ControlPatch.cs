﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using InnerNet;

using Object = UnityEngine.Object;
using AmongUs.GameOptions;

namespace MoreGamemodes
{
    [HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
    class ControllerManagerUpdatePatch
    {
        public static void Postfix()
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) && !Main.GameStarted)
                PlayerControl.LocalPlayer.GetComponent<CircleCollider2D>().enabled = !PlayerControl.LocalPlayer.gameObject.GetComponent<CircleCollider2D>().enabled;
            
            if (Input.GetKeyDown(KeyCode.Q) && Main.GameStarted && !PlayerControl.LocalPlayer.GetRole().CanUseKillButton() && PlayerControl.LocalPlayer.GetRole().ForceKillButton() && HudManager.Instance.KillButton.isActiveAndEnabled)
                HudManager.Instance.KillButton.DoClick();

            if (!AmongUsClient.Instance.AmHost) return;
            
            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.L, KeyCode.LeftShift }) && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started)
            {
                List<byte> winners = new();
                foreach (var pc in PlayerControl.AllPlayerControls)
                    winners.Add(pc.PlayerId);
                CheckEndCriteriaNormalPatch.StartEndGame(GameOverReason.HumansByVote, winners, CustomWinners.Terminated);
            }

            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.Z, KeyCode.LeftShift }) && Main.GameStarted && !PlayerControl.LocalPlayer.Data.IsDead &&
                !(CustomGamemode.Instance.Gamemode is Gamemodes.PaintBattle or Gamemodes.Speedrun or Gamemodes.Jailbreak or Gamemodes.BaseWars or Gamemodes.FreezeTag))
            {
                PlayerControl.LocalPlayer.RpcSetDeathReason(DeathReasons.Command);
                PlayerControl.LocalPlayer.RpcExileV2();
            }

            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.M, KeyCode.LeftShift }) && MeetingHud.Instance)
            {
                VotingCompletePatch.Postfix(MeetingHud.Instance, Array.Empty<MeetingHud.VoterState>(), null, true);
                MeetingHud.Instance.RpcClose();
            }

            if (Input.GetKeyDown(KeyCode.C) && GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown)
                GameStartManager.Instance.ResetStartState();

            if (Input.GetKeyDown(KeyCode.LeftShift) && GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown)
                GameStartManager.Instance.countDownTimer = 0f;

            if (GetKeysDown(new[] { KeyCode.LeftControl, KeyCode.Delete }) && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            {
                OptionItem.AllOptions.ToArray().Where(x => x.Id > 0).Do(x => x.CurrentValue = x.DefaultValue);
                GameManager.Instance.RpcSyncCustomOptions();
                var menu = Object.FindObjectOfType<GameOptionsMenu>();
                if (menu)
                    GameOptionsMenuPatch.ReOpenSettingMenu();
                var viewSettingsPane = Object.FindObjectOfType<LobbyViewSettingsPane>();
                if (viewSettingsPane != null)
                {
                    if (viewSettingsPane.currentTab != StringNames.OverviewCategory && viewSettingsPane.currentTab != StringNames.RolesCategory)
                        viewSettingsPane.RefreshTab();
                    viewSettingsPane.gameModeText.text = Options.Gamemode.GetString();
                }
            }

            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.N, KeyCode.LeftShift }) && Main.GameStarted)
            {
                CustomRpcSender sender = CustomRpcSender.Create("test", Hazel.SendOption.None);
                sender.StartMessage(-1);
                sender.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SetRole)
                    .Write((ushort)RoleTypes.Phantom)
                    .Write(true)
                    .EndRpc();
                sender.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.StartVanish)
                    .EndRpc();
                sender.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SetRole)
                    .Write((ushort)RoleTypes.Crewmate)
                    .Write(true)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
            }
        }

        static bool GetKeysDown(KeyCode[] keys)
        {
            if (keys.Any(k => Input.GetKeyDown(k)) && keys.All(k => Input.GetKey(k)))
            {
                return true;
            }
            return false;
        }
    }
}