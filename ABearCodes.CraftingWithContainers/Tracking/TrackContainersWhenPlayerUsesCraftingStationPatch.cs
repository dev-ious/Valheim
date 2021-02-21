﻿using System.Collections.Generic;
using System.Linq;
using ABearCodes.Valheim.CraftingWithContainers.Tracking;
using HarmonyLib;
using UnityEngine;

namespace ABearCodes.Valheim.CraftingWithContainers.Tracking
{
    [HarmonyPatch(typeof(Player), "SetCraftingStation")]
    public static class TrackContainersWhenPlayerUsesCraftingStationPatch
    {
        private static GameObject _connectionPrefab;

        private static GameObject ConnectionPrefab
        {
            get
            {
                if (_connectionPrefab == null)
                    _connectionPrefab = ZNetScene.instance
                        .GetPrefab("piece_workbench_ext1")
                        .GetComponent<StationExtension>()
                        .m_connectionPrefab;

                return _connectionPrefab;
            }
        }

        // ReSharper disable once UnusedMember.Global
        public static void Prefix(ref Player __instance, CraftingStation station)
        {
            if (station != null)
            {
                var containersInRange = FindAllowedContainersInCraftingStationRange(__instance, station).ToList();

                Valheim.CraftingWithContainers.Plugin.Log.LogDebug($"{__instance.GetPlayerName()} using {station.name}. " +
                                                                   $"Expanding with {containersInRange.Count}");
                var effects = Valheim.CraftingWithContainers.Plugin.Settings.ShowStationExtensionEffect.Value
                    ? SpawnEffects(station, containersInRange)
                    : new List<GameObject>();
                InventoryTracker.ExpandedPlayerInventories[__instance.GetInventory().GetHashCode()] =
                    new InventoryTracker.LinkedInventories(__instance, station, containersInRange, effects);
            }
            else
            {
                Valheim.CraftingWithContainers.Plugin.Log.LogDebug($"{__instance.GetPlayerName()} no longer using station, cleaning up");
                var effects = InventoryTracker
                    .ExpandedPlayerInventories[__instance.GetInventory().GetHashCode()]
                    .Effects;
                foreach (var effect in effects) Object.Destroy(effect);
                InventoryTracker.ExpandedPlayerInventories.Remove(__instance.GetInventory().GetHashCode());
            }
        }


        private static List<GameObject> SpawnEffects(CraftingStation station, List<Container> containersInRange)
        {
            var newEffects = new List<GameObject>();
            var stationPosition = station.transform.position;
            foreach (var container in containersInRange)
            {
                Valheim.CraftingWithContainers.Plugin.Log.LogDebug($"Attaching effect {container.name} ({container.GetHashCode()})");
                var containerPosition = container.transform.position + Vector3.up;
                var effect = Object.Instantiate(ConnectionPrefab, containerPosition, Quaternion.identity);
                var effectPosition = stationPosition - containerPosition;
                var quaternion = Quaternion.LookRotation(effectPosition.normalized);
                effect.transform.position = containerPosition;
                effect.transform.rotation = quaternion;
                effect.transform.localScale = new Vector3(1f, 1f, effectPosition.magnitude);
                newEffects.Add(effect);
            }

            return newEffects;
        }

        private static IEnumerable<Container> FindAllowedContainersInCraftingStationRange(Player player,
            CraftingStation craftingStation)
        {
            return Object.FindObjectsOfType<Container>()
                .Select(container => new
                {
                    Container = container,
                    OwningPiece = GetContainerOwningPiece(container)
                })
                .Where(context =>
                    ReversePatches.ContainerCheckAccess(context.Container, player.GetPlayerID())
                    && IsContainerInRange(craftingStation, context.Container)
                    && IsContainerOnAllowedPiece(context.OwningPiece)
                    && IsContainerOwningPiecePlacedByPlayer(context.OwningPiece))
                .Select(context => context.Container);
        }

        private static Piece GetContainerOwningPiece(Container container)
        {
            return (container.m_rootObjectOverride != null
                ? container.m_rootObjectOverride.gameObject
                : container.gameObject).GetComponent<Piece>();
        }

        private static bool IsContainerOnAllowedPiece(Piece owningPiece)
        {
            return !Valheim.CraftingWithContainers.Plugin.Settings.ShouldFilterByContainerPieceNames.Value ||
                   Valheim.CraftingWithContainers.Plugin.Settings.AllowedContainerLookupPieceNamesAsList
                       .Contains(owningPiece.m_name);
        }

        private static bool IsContainerOwningPiecePlacedByPlayer(Piece piece)
        {
            return piece.GetCreator() != 0;
        }

        private static bool IsContainerInRange(CraftingStation craftingStation, Container container)
        {
            return Vector3.Distance(craftingStation.transform.position,
                container.transform.position) < CalculateRange(craftingStation);
        }

        private static float CalculateRange(CraftingStation craftingStation)
        {
            return craftingStation.m_rangeBuild * Valheim.CraftingWithContainers.Plugin.Settings.ContainerLookupRangeMultiplier.Value;
        }
    }
}