﻿using System;
using System.Linq;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace WildlifeBegone {
	internal static class Patches {

		[HarmonyPatch(typeof(SpawnRegion), "Start", new Type[0])]
		private static class SpawnRegionPatch {

			private static readonly FieldInfo startHasBeenCalled = AccessTools.Field(typeof(SpawnRegion), "m_StartHasBeenCalled");

			private static void Prefix(SpawnRegion __instance) {
				bool m_StartHasBeenCalled = (bool) startHasBeenCalled.GetValue(__instance);
				if (m_StartHasBeenCalled || (GameManager.IsStoryMode() && !WildlifeBegone.Config.enableInStoryMode))
					return;

				GameObject spawnedObject = __instance.m_SpawnablePrefab;
				BaseAi ai = spawnedObject.GetComponent<BaseAi>();
				if (ai == null)
					return;

				SpawnRateSetting spawnRates = WildlifeBegone.Config.spawnRates[(int) ai.m_AiSubType];
				AdjustRegion(__instance, spawnRates);
			}

			private static void AdjustRegion(SpawnRegion region, SpawnRateSetting spawnRates) {
				float activeMultiplier = spawnRates.SpawnRegionActiveTimeMultiplier;
				float respawnMultiplier = spawnRates.MaximumRespawnsPerDayMultiplier;
				float maximumCountMultiplier = spawnRates.MaximumSpawnedAnimalsMultiplier;

				float oldChanceActive = region.m_ChanceActive;
				region.m_ChanceActive *= activeMultiplier;

				float oldRespawnTime = region.m_MaxRespawnsPerDayStalker;
				region.m_MaxRespawnsPerDayPilgrim *= respawnMultiplier;
				region.m_MaxRespawnsPerDayVoyageur *= respawnMultiplier;
				region.m_MaxRespawnsPerDayStalker *= respawnMultiplier;
				region.m_MaxRespawnsPerDayInterloper *= respawnMultiplier;

				int oldMaximumCountDay = region.m_MaxSimultaneousSpawnsDayStalker;
				int oldMaximumCountNight = region.m_MaxSimultaneousSpawnsNightStalker;
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsDayPilgrim, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsDayVoyageur, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsDayStalker, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsDayInterloper, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsNightPilgrim, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsNightVoyageur, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsNightStalker, maximumCountMultiplier);
				RoundingMultiply(ref region.m_MaxSimultaneousSpawnsNightInterloper, maximumCountMultiplier);

				if (WildlifeBegone.Config.logging) {
					Debug.LogFormat("Adjusted spawner {0}: Active chance {1:F1} -> {2:F1}, respawns / day {3:F2} -> {4:F2}, maximum spawns ({5:D}, {6:D}) -> ({7:D}, {8:D})",
						region.name,
						oldChanceActive, region.m_ChanceActive,
						oldRespawnTime, region.m_MaxRespawnsPerDayStalker,
						oldMaximumCountDay, oldMaximumCountNight, region.m_MaxSimultaneousSpawnsDayStalker, region.m_MaxSimultaneousSpawnsNightStalker
					);
				}
			}
		}

		[HarmonyPatch(typeof(RandomSpawnObject), "Start", new Type[0])]
		private static class RandomSpawnObjectPatch {
			private static void Prefix(RandomSpawnObject __instance) {
				if (!IsSpawnerRSO(__instance))
					return;
				if (GameManager.IsStoryMode() && !WildlifeBegone.Config.enableInStoryMode)
					return;

				float oldRerollTime = __instance.m_RerollAfterGameHours;
				int oldMaxObjects = __instance.m_NumObjectsToEnableStalker;

				RSOSettings rsoSettings = WildlifeBegone.Config.rsoSettings;
				float maximumCountMultiplier = rsoSettings.ActiveSpawnerCountMultiplier;

				__instance.m_RerollAfterGameHours *= rsoSettings.RerollActiveSpawnersTimeMultiplier;
				RoundingMultiply(ref __instance.m_NumObjectsToEnablePilgrim, maximumCountMultiplier);
				RoundingMultiply(ref __instance.m_NumObjectsToEnableVoyageur, maximumCountMultiplier);
				RoundingMultiply(ref __instance.m_NumObjectsToEnableStalker, maximumCountMultiplier);
				RoundingMultiply(ref __instance.m_NumObjectsToEnableInterloper, maximumCountMultiplier);

				if (WildlifeBegone.Config.logging) {
					Debug.LogFormat("Adjusted RSO {0}: Reroll time {1:F1} -> {2:F1}, maximum active {3:D} -> {4:D}",
							__instance.name,
							oldRerollTime, __instance.m_RerollAfterGameHours,
							oldMaxObjects, __instance.m_NumObjectsToEnableStalker);
				}
			}

			private static bool IsSpawnerRSO(RandomSpawnObject rso) {
				foreach (GameObject go in rso.m_ObjectList) {
					if (go && !go.GetComponent<SpawnRegion>())
						return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(ConsoleManager), "RegisterCommands", new Type[0])]
		private static class AddConsoleCommands {
			private static void Postfix() {
				uConsole.RegisterCommand("animals_count", new uConsole.DebugCommand(CountAnimals));
				uConsole.RegisterCommand("animals_kill_all", new uConsole.DebugCommand(KillAllAnimals));
			}

			private const int numAnimalTypes = 6;

			private static void CountAnimals() {
				int[] counts = new int[numAnimalTypes];
				BaseAi[] animals = UnityEngine.Object.FindObjectsOfType<BaseAi>();
				foreach (BaseAi animal in animals) {
					if (animal.GetAiMode() != AiMode.Dead) {
						int ordinal = (int) animal.m_AiSubType;
						if (ordinal >= numAnimalTypes)
							ordinal = 0;
						++counts[ordinal];
					}
				}

				object[] args = counts.Cast<object>().ToArray();
				Debug.LogFormat("{2} bears, {4} rabbits, {3} deer, {1} wolves, {5} moose, {0} unknown", args);
			}

			private static void KillAllAnimals() {
				int[] counts = new int[numAnimalTypes];
				BaseAi[] animals = UnityEngine.Object.FindObjectsOfType<BaseAi>();
				foreach (BaseAi animal in animals) {
					if (animal.GetAiMode() != AiMode.Dead) {
						animal.SetAiMode(AiMode.Dead);
						animal.Despawn();

						int ordinal = (int) animal.m_AiSubType;
						if (ordinal >= numAnimalTypes)
							ordinal = 0;
						++counts[ordinal];
					}
				}

				object[] args = counts.Cast<object>().ToArray();
				Debug.LogFormat("Killed {2} bears, {4} rabbits, {3} deer, {1} wolves, {5} moose, {0} unknown", args);
			}
		}

		private static void RoundingMultiply(ref int field, float multiplier) {
			field = Math.Max(1, Mathf.RoundToInt(field * multiplier));
		}
	}
}
