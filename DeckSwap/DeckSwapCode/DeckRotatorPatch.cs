using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using System.Linq;

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

            // Snapshot each player's serialized state and extract decks
            var serializedPlayers = players.Select(p => p.ToSerializable()).ToList();
            var decks = serializedPlayers.Select(sp => sp.Deck).ToList();

            // Rotate: P1 gets P2's deck, P2 gets P3's, P3 gets P1's
            for (int i = 0; i < players.Count; i++)
            {
                int sourceIndex = (i + 1) % players.Count;
                var player = players[i];

                if (LocalContext.IsMe(player))
                {
                    // For local player: only swap the deck directly
                    // to avoid breaking potion/relic UI state
                    player.Deck.Clear(silent: true);
                    foreach (var serializedCard in decks[sourceIndex])
                    {
                        var card = CapturedRunState.LoadCard(serializedCard, player);
                        player.Deck.AddInternal(card, silent: true);
                    }
                    MainFile.Logger.Info($"DeckSwap: swapped local player {player.NetId} deck directly");
                }
                else
                {
                    // For remote players: safe to use SyncWithSerializedPlayer
                    serializedPlayers[i].Deck = decks[sourceIndex];
                    player.SyncWithSerializedPlayer(serializedPlayers[i]);
                    MainFile.Logger.Info($"DeckSwap: synced remote player {player.NetId} via SyncWithSerializedPlayer");
                }
            }

            MainFile.Logger.Info($"DeckSwap: rotated decks for {players.Count} players");
        }
    }
}