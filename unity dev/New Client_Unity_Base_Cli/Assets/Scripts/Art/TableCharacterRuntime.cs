using System.Collections;
using UnityEngine;

namespace Cabo.Client.Art
{
    public sealed class TableCharacterRuntime : MonoBehaviour
    {
        const int TextureWidth = 320;
        const int TextureHeight = 400;
        const int RenderLayer = 31;

        Camera _camera;
        RenderTexture _output;
        Transform _actorRoot;
        SettlementCharacterActor _actor;
        Coroutine _reaction;
        string _characterId;
        Vector3 _actorBaseScale = Vector3.one;
        bool _initialized;

        public RenderTexture Output => _output;

        public static TableCharacterRuntime Create(Transform parent)
        {
            var go = new GameObject("TableCharacterStage");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var runtime = go.AddComponent<TableCharacterRuntime>();
            runtime.EnsureInitialized();
            return runtime;
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            gameObject.layer = RenderLayer;
            transform.position = new Vector3(0f, 1200f, 0f);
            _actorRoot = new GameObject("Actor").transform;
            _actorRoot.SetParent(transform, false);
            _actorRoot.gameObject.layer = RenderLayer;

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
            cameraObject.layer = RenderLayer;
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 2.25f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cullingMask = 1 << RenderLayer;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.targetTexture = _output;
        }

        public void Show(string characterId)
        {
            EnsureInitialized();
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
            if (_actor == null)
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
            _actorBaseScale = Vector3.one * GetTableScale(characterId);
            actorObject.transform.localScale = _actorBaseScale;
            SetLayerRecursively(actorObject, RenderLayer);
            _actor = actorObject.GetComponent<SettlementCharacterActor>();
            _actor?.ResetPose();
        }

        static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
                return;

            target.layer = layer;
            var targetTransform = target.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
                SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
        }

        IEnumerator ReactionRoutine(int delta)
        {
            if (delta <= 0)
                yield return _actor.PlayRewardReaction();
            else
                yield return _actor.PlayPenaltyReaction();

            _actor?.ResetPose();
            _reaction = null;
        }

        static float GetTableScale(string characterId)
        {
            if (string.Equals(characterId, "trainee", System.StringComparison.OrdinalIgnoreCase))
                return 1.22f;
            if (string.Equals(characterId, "milkdragon", System.StringComparison.OrdinalIgnoreCase))
                return 1.28f;
            return 0.94f;
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
