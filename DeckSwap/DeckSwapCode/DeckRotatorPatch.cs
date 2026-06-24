using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using System.Linq;

namespace DeckSwap.DeckSwapCode;

public class DeckRotatorPatch
{
    public static RunState? CapturedRunState { get; set; }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
    public class RunStateCapturer
    {
        static void Postfix(RunState __result)
        {
            CapturedRunState = __result;
            MainFile.Logger.Info("DeckSwap: run started, RunState captured");
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    public class RunStateCleanup
    {
        static void Prefix()
        {
            CapturedRunState = null;
            MainFile.Logger.Info("DeckSwap: run ended, RunState cleared");
        }
    }

    [HarmonyPatch(typeof(CombatStateSynchronizer), nameof(CombatStateSynchronizer.StartSync))]
    public class DeckRotator
    {
        static void Prefix()
        {
            if (CapturedRunState == null) return;

            var players = CapturedRunState.Players;
            if (players.Count < 2) return;

            // Sort by NetId so every client agrees on the same rotation order
            // regardless of what order the local player list is in
            var sortedPlayers = players.OrderBy(p => p.NetId).ToList();

            MainFile.Logger.Info($"DeckSwap: sorted player order: {string.Join(", ", sortedPlayers.Select(p => p.NetId))}");

            // Snapshot ALL decks in sorted order before any changes
            var serializedDecks = sortedPlayers
                .Select(p => p.Deck.Cards
                    .Select(c => c.ToSerializable())
                    .ToList())
                .ToList();

            // Find the local player's index in the sorted list
            // and swap only their deck
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                if (!LocalContext.IsMe(sortedPlayers[i])) continue;

                int sourceIndex = (i + 1) % sortedPlayers.Count;
                var localPlayer = sortedPlayers[i];

                MainFile.Logger.Info($"DeckSwap: local player index={i}, taking deck from index={sourceIndex} (NetId={sortedPlayers[sourceIndex].NetId})");

                localPlayer.Deck.Clear(silent: true);

                foreach (var serializedCard in serializedDecks[sourceIndex])
                {
                    var card = CapturedRunState.LoadCard(serializedCard, localPlayer);
                    localPlayer.Deck.AddInternal(card, silent: true);
                }

                MainFile.Logger.Info($"DeckSwap: swapped local player {localPlayer.NetId} deck successfully");
                break;
            }
        }
    }
}