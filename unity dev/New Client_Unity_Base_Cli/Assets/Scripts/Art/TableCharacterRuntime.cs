using System.Collections;
using UnityEngine;

namespace Cabo.Client.Art
{
    public sealed class TableCharacterRuntime : MonoBehaviour
    {
        const int TextureWidth = 320;
        const int TextureHeight = 400;

        Camera _camera;
        RenderTexture _output;
        Transform _actorRoot;
        SettlementCharacterActor _actor;
        Coroutine _reaction;
        string _characterId;
        Vector3 _actorBaseScale = Vector3.one;

        public RenderTexture Output => _output;

        public static TableCharacterRuntime Create(Transform parent)
        {
            var go = new GameObject("TableCharacterStage");
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<TableCharacterRuntime>();
        }

        void Awake()
        {
            transform.position = new Vector3(0f, 1200f, 0f);
            _actorRoot = new GameObject("Actor").transform;
            _actorRoot.SetParent(transform, false);

            _output = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "CaboTableCharacter",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _output.Create();

            var cameraObject = new GameObject("TableCharacterCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.12f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 2.25f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.targetTexture = _output;
        }

        public void Show(string characterId)
        {
            gameObject.SetActive(true);
            string normalized = string.IsNullOrWhiteSpace(characterId) ? "pomelo" : characterId;
            if (_actor == null || !string.Equals(_characterId, normalized, System.StringComparison.OrdinalIgnoreCase))
                Rebuild(normalized);
        }

        public void Hide()
        {
            StopReaction();
            gameObject.SetActive(false);
        }

        public void ReactToHandChange(int delta)
        {
            if (_actor == null || delta == 0)
                return;

            StopReaction();
            _reaction = StartCoroutine(ReactionRoutine(delta));
        }

        void Rebuild(string characterId)
        {
            StopReaction();
            for (int i = _actorRoot.childCount - 1; i >= 0; i--)
                Destroy(_actorRoot.GetChild(i).gameObject);

            _actor = null;
            _characterId = characterId;
            var character = CaboArt.GetCharacter(characterId) ?? CaboArt.GetCharacter("pomelo");
            if (character?.settlementPrefab == null)
            {
                Debug.LogWarning($"[TableCharacter] Settlement prefab is missing for character '{characterId}'.");
                return;
            }

            var actorObject = Instantiate(character.settlementPrefab, _actorRoot);
            actorObject.name = "TableCharacterActor";
            actorObject.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            _actorBaseScale = Vector3.one * 0.94f;
            actorObject.transform.localScale = _actorBaseScale;
            _actor = actorObject.GetComponent<SettlementCharacterActor>();
            _actor?.ResetPose();
        }

        IEnumerator ReactionRoutine(int delta)
        {
            if (delta < 0)
                yield return _actor.PlayRewardReaction();
            else
                yield return _actor.PlayPenaltyReaction();

            _actor?.ResetPose();
            _reaction = null;
        }

        void Update()
        {
            if (_actor == null)
                return;

            float breathing = 1f + Mathf.Sin(Time.unscaledTime * 1.8f) * 0.012f;
            _actor.transform.localScale = _actorBaseScale * breathing;
        }

        void StopReaction()
        {
            if (_reaction != null)
                StopCoroutine(_reaction);
            _reaction = null;
            _actor?.ResetPose();
        }

        void OnDestroy()
        {
            StopReaction();
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
