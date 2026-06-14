using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cabo.Client.Art
{
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

        public RenderTexture Output => _output;

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

        public void Play(int roundNumber, IReadOnlyList<RoundResult> results)
        {
            if (_roundNumber == roundNumber && _actors.Count > 0)
                return;

            _roundNumber = roundNumber;
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
                if (actor != null)
                    _actors.Add(actor);
            }
        }

        IEnumerator PlayResults(IReadOnlyList<RoundResult> results)
        {
            if (results == null || results.Count == 0 || _actors.Count == 0)
                yield break;

            var routines = new List<Coroutine>();
            for (int i = 0; i < _actors.Count && i < results.Count; i++)
                routines.Add(StartCoroutine(PlayActorCards(_actors[i], results[i].CardValues)));
            for (int i = 0; i < routines.Count; i++)
                yield return routines[i];
            _playback = null;
        }

        IEnumerator PlayActorCards(SettlementCharacterActor actor, IReadOnlyList<int> values)
        {
            if (actor == null || values == null)
                yield break;

            bool played = false;
            for (int i = 0; i < values.Count; i++)
            {
                var food = CaboArt.GetFood(values[i]);
                if (food?.consumeSprite == null)
                    continue;
                played = true;
                yield return actor.PlayFood(food);
            }

            if (!played)
                actor.ResetPose();
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
