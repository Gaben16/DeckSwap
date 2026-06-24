using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using System.Collections.Generic;
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

            // Snapshot each player's deck as serialized cards
            var serializedDecks = players
                .Select(p => p.Deck.Cards
                    .Select(c => c.ToSerializable())
                    .ToList())
                .ToList();

            // Rotate: P1 gets P2's cards, P2 gets P3's, P3 gets P1's
            for (int i = 0; i < players.Count; i++)
            {
                int sourceIndex = (i + 1) % players.Count;
                var player = players[i];

                // Clear current deck silently
                player.Deck.Clear(silent: true);

                // Deserialize cards fresh for this specific player
                foreach (var serializedCard in serializedDecks[sourceIndex])
                {
                    var card = CapturedRunState.LoadCard(serializedCard, player);
                    player.Deck.AddInternal(card, silent: true);
                }
            }

            MainFile.Logger.Info($"DeckSwap: rotated decks for {players.Count} players");
        }
    }
}