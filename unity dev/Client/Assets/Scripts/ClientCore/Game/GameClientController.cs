using System;
using Game.Game;
using UnityEngine;

namespace Cabo.Client.Game
{
    /// <summary>
    /// Handles game-phase protocol messages from the server.
    /// MVP: logs all events and fires C# events for future UI binding.
    /// Does NOT make rule decisions — server is authoritative.
    /// </summary>
    public sealed class GameClientController : MonoBehaviour
    {
        // Events for UI layer
        public event Action<GameStartNotify> GameStarted;
        public event Action<TurnStartNotify> TurnStarted;
        public event Action<ActionResultNotify> ActionResulted;
        public event Action<RoundRevealNotify> RoundRevealed;
        public event Action<ScoreUpdateNotify> ScoreUpdated;
        public event Action<GameOverNotify> GameOvered;

        // Response callbacks (one-shot)
        public event Action<DrawCardRsp> DrawCardResponsed;
        public event Action<DiscardDrawnRsp> DiscardDrawnResponsed;
        public event Action<ReplaceWithDrawnRsp> ReplaceWithDrawnResponsed;
        public event Action<TakeFromDiscardRsp> TakeFromDiscardResponsed;
        public event Action<UseSkillRsp> UseSkillResponsed;
        public event Action<CallSteadyRsp> CallSteadyResponsed;

        public void HandleGameStart(GameStartNotify notify)
        {
            Debug.Log($"[GameClientController] Game started: round={notify.RoundNumber}, firstPlayer={notify.FirstPlayerId}");
            GameStarted?.Invoke(notify);
        }

        public void HandleTurnStart(TurnStartNotify notify)
        {
            Debug.Log($"[GameClientController] Turn start: player={notify.CurrentPlayerId}, turn={notify.TurnNumber}, round={notify.RoundNumber}");
            TurnStarted?.Invoke(notify);
        }

        public void HandleDrawCardRsp(DrawCardRsp rsp)
        {
            Debug.Log($"[GameClientController] DrawCardRsp: card={rsp.CardId}, value={rsp.Value}, skill={rsp.Skill}");
            DrawCardResponsed?.Invoke(rsp);
        }

        public void HandleDiscardDrawnRsp(DiscardDrawnRsp rsp)
        {
            Debug.Log($"[GameClientController] DiscardDrawnRsp: ok");
            DiscardDrawnResponsed?.Invoke(rsp);
        }

        public void HandleReplaceWithDrawnRsp(ReplaceWithDrawnRsp rsp)
        {
            Debug.Log($"[GameClientController] ReplaceWithDrawnRsp: success={rsp.ExchangeResult?.Success}");
            ReplaceWithDrawnResponsed?.Invoke(rsp);
        }

        public void HandleTakeFromDiscardRsp(TakeFromDiscardRsp rsp)
        {
            Debug.Log($"[GameClientController] TakeFromDiscardRsp: success={rsp.ExchangeResult?.Success}");
            TakeFromDiscardResponsed?.Invoke(rsp);
        }

        public void HandleUseSkillRsp(UseSkillRsp rsp)
        {
            Debug.Log($"[GameClientController] UseSkillRsp: peeked={rsp.PeekedValue}, swap={rsp.SwapOccurred}");
            UseSkillResponsed?.Invoke(rsp);
        }

        public void HandleCallSteadyRsp(CallSteadyRsp rsp)
        {
            Debug.Log($"[GameClientController] CallSteadyRsp: ok");
            CallSteadyResponsed?.Invoke(rsp);
        }

        public void HandleActionResult(ActionResultNotify notify)
        {
            Debug.Log($"[GameClientController] Action result: type={notify.ActionType}, player={notify.SourcePlayerId}, target={notify.TargetPlayerId}, turnEnded={notify.TurnEnded}");
            ActionResulted?.Invoke(notify);
        }

        public void HandleRoundReveal(RoundRevealNotify notify)
        {
            Debug.Log($"[GameClientController] Round reveal: round={notify.RoundNumber}, caller={notify.SteadyCallerId}");
            RoundRevealed?.Invoke(notify);
        }

        public void HandleScoreUpdate(ScoreUpdateNotify notify)
        {
            Debug.Log($"[GameClientController] Score update: round={notify.RoundNumber}, triggers={notify.HundredTriggers?.Count ?? 0}");
            ScoreUpdated?.Invoke(notify);
        }

        public void HandleGameOver(GameOverNotify notify)
        {
            Debug.Log($"[GameClientController] Game over! rounds={notify.TotalRounds}, rankings={notify.Rankings?.Count ?? 0}");
            GameOvered?.Invoke(notify);
        }
    }
}
