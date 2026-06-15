using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Cabo.Client.Art
{
    public enum SettlementCueType
    {
        FoodStarted,
        FoodConsumed,
        Penalty,
        KamikazeTriggered,
        KamikazePenalty,
        ResultFinalized
    }

    public readonly struct SettlementCue
    {
        public readonly SettlementCueType Type;
        public readonly int PlayerIndex;
        public readonly int CardValue;
        public readonly int RunningHandTotal;
        public readonly int Amount;

        public SettlementCue(SettlementCueType type, int playerIndex, int cardValue = 0,
            int runningHandTotal = 0, int amount = 0)
        {
            Type = type;
            PlayerIndex = playerIndex;
            CardValue = cardValue;
            RunningHandTotal = runningHandTotal;
            Amount = amount;
        }
    }

    public sealed class SettlementStageRuntime : MonoBehaviour
    {
        const int TextureWidth = 1024;
        const int TextureHeight = 420;

        readonly List<SettlementCharacterActor> _actors = new();
        Camera _camera;
        RenderTexture _output;
        Coroutine _playback;
        Transform _actorRoot;
        int _roundNumber = -1;
        int _completedRoundNumber = -1;
        Action<SettlementCue> _cueHandler;
        Action _completeHandler;

        public RenderTexture Output => _output;

        public Vector2 GetActorNameViewportPosition(int actorIndex)
        {
            if (_camera == null || actorIndex < 0 || actorIndex >= _actors.Count || _actors[actorIndex] == null)
                return new Vector2(0.5f, 0.12f);

            Vector3 point = _camera.WorldToViewportPoint(_actors[actorIndex].NameAnchorWorldPosition);
            return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
        }

        public static SettlementStageRuntime Create(Transform parent)
        {
            var go = new GameObject("SettlementCharacterStage");
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<SettlementStageRuntime>();
        }

        void Awake()
        {
            transform.position = new Vector3(0f, 1000f, 0f);
            _actorRoot = new GameObject("Actors").transform;
            _actorRoot.SetParent(transform, false);

            _output = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "CaboSettlementCharacters",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _output.Create();

            var cameraObject = new GameObject("SettlementCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.15f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 2.55f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.targetTexture = _output;
        }

        public void Play(int roundNumber, IReadOnlyList<RoundResult> results,
            Action<SettlementCue> cueHandler = null, Action completeHandler = null)
        {
            _cueHandler = cueHandler;
            _completeHandler = completeHandler;
            if (_roundNumber == roundNumber && _actors.Count > 0)
            {
                if (_playback == null && _completedRoundNumber == roundNumber)
                    _completeHandler?.Invoke();
                return;
            }

            _roundNumber = roundNumber;
            _completedRoundNumber = -1;
            RebuildActors(results?.Count ?? 0, results);
            if (_playback != null)
                StopCoroutine(_playback);
            _playback = StartCoroutine(PlayResults(results));
        }

        public void PlayPilotPreview()
        {
            _roundNumber--;
            RebuildActors(1, null);
            if (_playback != null)
                StopCoroutine(_playback);
            _playback = StartCoroutine(PlayPilotLoop());
        }

        public void StopPlayback()
        {
            if (_playback != null)
                StopCoroutine(_playback);
            _playback = null;
            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i] != null)
                    _actors[i].StopPlayback();
            }
        }

        void RebuildActors(int requestedCount, IReadOnlyList<RoundResult> results)
        {
            for (int i = _actorRoot.childCount - 1; i >= 0; i--)
                Destroy(_actorRoot.GetChild(i).gameObject);
            _actors.Clear();

            int count = Mathf.Clamp(requestedCount, 1, 4);
            float spacing = count == 1 ? 0f : 2.05f;
            float start = -spacing * (count - 1) * 0.5f;
            float scale = count == 1 ? 1.18f : count == 2 ? 0.96f : 0.74f;
            for (int i = 0; i < count; i++)
            {
                string characterId = results != null && i < results.Count
                    ? results[i].CharacterId
                    : "pomelo";
                var character = CaboArt.GetCharacter(characterId) ?? CaboArt.GetCharacter("pomelo");
                if (character?.settlementPrefab == null)
                {
                    Debug.LogWarning($"[SettlementStage] Settlement prefab is missing for character '{characterId}'.");
                    _actors.Add(null);
                    continue;
                }

                var go = Instantiate(character.settlementPrefab, _actorRoot);
                go.name = $"SettlementActor_{i + 1}";
                go.transform.localPosition = new Vector3(start + spacing * i, -0.25f, 0f);
                go.transform.localScale = Vector3.one * scale;
                var actor = go.GetComponent<SettlementCharacterActor>();
                _actors.Add(actor);
            }
        }

        IEnumerator PlayResults(IReadOnlyList<RoundResult> results)
        {
            if (results == null || results.Count == 0 || _actors.Count == 0)
            {
                CompletePlayback();
                yield break;
            }

            int kamikazeIndex = -1;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].IsKamikaze)
                {
                    kamikazeIndex = i;
                    break;
                }
            }

            var routines = new List<Coroutine>();
            for (int i = 0; i < _actors.Count && i < results.Count; i++)
                routines.Add(StartCoroutine(PlayActorResult(_actors[i], results[i], i, kamikazeIndex >= 0)));
            for (int i = 0; i < routines.Count; i++)
                yield return routines[i];

            if (kamikazeIndex >= 0)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                CaboAudio.Play(CaboSfx.Skill, 0.85f);
                Emit(new SettlementCue(SettlementCueType.KamikazeTriggered, kamikazeIndex));
                if (kamikazeIndex < _actors.Count && _actors[kamikazeIndex] != null)
                    yield return _actors[kamikazeIndex].PlayRewardReaction();

                for (int i = 0; i < results.Count; i++)
                {
                    if (i == kamikazeIndex)
                        continue;
                    int amount = results[i].Penalty > 0 ? results[i].Penalty : 50;
                    CaboAudio.Play(CaboSfx.Penalty, 0.78f);
                    Emit(new SettlementCue(SettlementCueType.KamikazePenalty, i, amount: amount));
                    if (i < _actors.Count && _actors[i] != null)
                        yield return _actors[i].PlayPenaltyReaction();
                }

                for (int i = 0; i < results.Count; i++)
                    Emit(new SettlementCue(SettlementCueType.ResultFinalized, i));
            }

            CompletePlayback();
        }

        IEnumerator PlayActorResult(SettlementCharacterActor actor, RoundResult result, int playerIndex,
            bool suppressPenalty)
        {
            if (result == null)
                yield break;

            bool played = false;
            int runningHandTotal = 0;
            var values = result.CardValues;
            for (int i = 0; values != null && i < values.Count; i++)
            {
                var food = CaboArt.GetFood(values[i]);
                if (food?.consumeSprite == null)
                    continue;
                played = true;
                Emit(new SettlementCue(SettlementCueType.FoodStarted, playerIndex, values[i], runningHandTotal));
                if (actor != null)
                    yield return actor.PlayFood(food);
                else
                    yield return new WaitForSecondsRealtime(0.2f);
                runningHandTotal += values[i];
                CaboAudio.Play(CaboSfx.Eat, 0.66f);
                Emit(new SettlementCue(SettlementCueType.FoodConsumed, playerIndex, values[i], runningHandTotal));
            }

            if (!played && actor != null)
                actor.ResetPose();

            if (!suppressPenalty && result.Penalty > 0)
            {
                yield return new WaitForSecondsRealtime(0.18f);
                CaboAudio.Play(CaboSfx.Penalty, 0.78f);
                Emit(new SettlementCue(SettlementCueType.Penalty, playerIndex, amount: result.Penalty));
                if (actor != null)
                    yield return actor.PlayPenaltyReaction();
            }

            if (!suppressPenalty)
                Emit(new SettlementCue(SettlementCueType.ResultFinalized, playerIndex));
        }

        void Emit(SettlementCue cue)
        {
            _cueHandler?.Invoke(cue);
        }

        void CompletePlayback()
        {
            _playback = null;
            _completedRoundNumber = _roundNumber;
            _completeHandler?.Invoke();
        }

        IEnumerator PlayPilotLoop()
        {
            if (_actors.Count == 0)
                yield break;

            int[] pilotValues = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            while (true)
            {
                for (int i = 0; i < pilotValues.Length; i++)
                {
                    yield return _actors[0].PlayFood(CaboArt.GetFood(pilotValues[i]));
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
        }

        void OnDestroy()
        {
            StopPlayback();
            if (_camera != null)
                _camera.targetTexture = null;
            if (_output != null)
            {
                _output.Release();
                Destroy(_output);
            }
        }
    }
}
