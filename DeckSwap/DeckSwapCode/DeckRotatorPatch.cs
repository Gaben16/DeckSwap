using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace DeckSwap.DeckSwapCode;

public class DeckRotatorPatch
{
    public static RunState? CapturedRunState { get; set; }

    // Capture the RunState when a run starts
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
    public class RunStateCapturer
    {
        static void Postfix(RunState __result)
        {
            CapturedRunState = __result;
            MainFile.Logger.Info("DeckSwap: run started, RunState captured");
        }
    }

    // Clear the RunState when a run ends
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    public class RunStateCleanup
    {
        static void Prefix()
        {
            CapturedRunState = null;
            MainFile.Logger.Info("DeckSwap: run ended, RunState cleared");
        }
    }

    // Rotate decks just before each room's combat sync
    [HarmonyPatch(typeof(CombatStateSynchronizer), nameof(CombatStateSynchronizer.StartSync))]
    public class DeckRotator
    {
        static void Prefix()
        {
            if (CapturedRunState == null) return;

            var players = CapturedRunState.Players;
            if (players.Count < 2) return;

            // Snapshot each player's serialized state
            var serializedPlayers = players.Select(p => p.ToSerializable()).ToList();

            // Extract just the decks
            var decks = serializedPlayers.Select(sp => sp.Deck).ToList();

            // Rotate: P1 gets P2's deck, P2 gets P3's, P3 gets P1's
            for (int i = 0; i < players.Count; i++)
            {
                int sourceIndex = (i + 1) % players.Count;
                serializedPlayers[i].Deck = decks[sourceIndex];
                players[i].SyncWithSerializedPlayer(serializedPlayers[i]);
            }

            MainFile.Logger.Info($"DeckSwap: rotated decks for {players.Count} players");
        }
    }
}