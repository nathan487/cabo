using System.Collections;
using UnityEngine;

namespace Cabo.Client.Art
{
    public sealed class SettlementCharacterActor : MonoBehaviour
    {
        [Header("Rig")]
        public Transform visualRoot;
        public Transform bodyBone;
        public Transform headBone;
        public Transform leftShoulder;
        public Transform leftElbow;
        public Transform leftWrist;
        public Transform rightShoulder;
        public Transform rightElbow;
        public Transform rightWrist;
        public bool singleSegmentArms;
        public Vector2 leftRestTarget = new Vector2(-0.92f, -0.30f);
        public Vector2 rightRestTarget = new Vector2(0.92f, -0.30f);
        public Vector2 nameAnchorOffset = new Vector2(0f, 1.12f);

        [Header("Face")]
        public SpriteRenderer leftEye;
        public SpriteRenderer rightEye;
        public SpriteRenderer mouth;
        public Sprite leftEyeOpen;
        public Sprite rightEyeOpen;
        public Sprite leftEyeClosed;
        public Sprite rightEyeClosed;
        public Sprite mouthNeutral;
        public Sprite mouthEat;
        public Sprite mouthChew;
        public Sprite mouthHappy;
        public Sprite mouthFail;

        [Header("Hands and prop")]
        public SpriteRenderer leftHand;
        public SpriteRenderer rightHand;
        public Sprite leftHandRelaxed;
        public Sprite rightHandRelaxed;
        public Sprite leftHandGrip;
        public Sprite rightHandGrip;
        public Sprite leftHandRaised;
        public Sprite rightHandRaised;
        public SpriteRenderer propRenderer;
        public SpriteRenderer idlePropRenderer;

        [Header("Food poses")]
        public Vector2 bowlLeftTarget = new Vector2(-0.38f, 0.42f);
        public Vector2 bowlRightTarget = new Vector2(0.38f, 0.42f);
        public Vector2 bowlPropPosition = new Vector2(0f, 0.90f);
        public float bowlPropScale = 0.34f;
        public Vector2 drinkRightTarget = new Vector2(0.24f, 1.03f);
        public Vector2 drinkPropPosition = new Vector2(0.24f, 1.08f);
        public float drinkPropScale = 0.30f;
        public float drinkPropRotation = 12f;
        public Vector2 handheldLeftTarget = new Vector2(-0.22f, 0.78f);
        public Vector2 handheldRightTarget = new Vector2(0.26f, 0.98f);
        public Vector2 handheldPropPosition = new Vector2(0.04f, 1.00f);
        public float handheldPropScale = 0.30f;
        public float handheldPropRotation = -8f;
        public Vector2 propEnterOffset = new Vector2(0.72f, 0.66f);
        public float propScaleMultiplier = 1.4f;
        public float animationDurationMultiplier = 1.6f;

        [Header("Rig dimensions")]
        public float upperArmLength = 0.48f;
        public float forearmLength = 0.58f;

        [Header("Game over")]
        [SerializeField] Sprite gameOverDefeatSprite;
        public float gameOverDefeatScale = 0.68f;

        Coroutine _routine;
        SpriteRenderer _gameOverDefeatRenderer;
        Vector2 _leftTarget;
        Vector2 _rightTarget;
        Vector3 _bodyStart;
        Vector3 _headStart;
        Vector3 _bodyStartScale;
        Vector3 _visualRootStartScale;
        Quaternion _leftHandRestRotation;
        Quaternion _rightHandRestRotation;
        bool _leftHandIsRaised;
        bool _rightHandIsRaised;

        public Vector3 NameAnchorWorldPosition => headBone != null
            ? headBone.TransformPoint(new Vector3(nameAnchorOffset.x, nameAnchorOffset.y, 0f))
            : transform.position + Vector3.up * 2f;

        void Awake()
        {
            if (bodyBone != null) _bodyStart = bodyBone.localPosition;
            if (bodyBone != null) _bodyStartScale = bodyBone.localScale;
            if (headBone != null) _headStart = headBone.localPosition;
            if (visualRoot != null) _visualRootStartScale = visualRoot.localScale;
            if (leftHand != null) _leftHandRestRotation = leftHand.transform.localRotation;
            if (rightHand != null) _rightHandRestRotation = rightHand.transform.localRotation;
            ResetPose();
        }

        void LateUpdate()
        {
            SolveArm(leftShoulder, leftElbow, _leftTarget, true);
            SolveArm(rightShoulder, rightElbow, _rightTarget, false);
            if (_leftHandIsRaised && leftHand != null)
                leftHand.transform.rotation = Quaternion.identity;
            if (_rightHandIsRaised && rightHand != null)
                rightHand.transform.rotation = Quaternion.identity;
        }

        public void ResetPose()
        {
            _leftTarget = leftRestTarget;
            _rightTarget = rightRestTarget;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
                visualRoot.localScale = _visualRootStartScale;
            }
            if (bodyBone != null)
            {
                bodyBone.localPosition = _bodyStart;
                bodyBone.localScale = _bodyStartScale;
            }
            if (headBone != null) headBone.localPosition = _headStart;
            if (leftEye != null) leftEye.sprite = leftEyeOpen;
            if (rightEye != null) rightEye.sprite = rightEyeOpen;
            if (mouth != null) mouth.sprite = mouthNeutral;
            SetRaisedHands(false, false);
            if (idlePropRenderer != null)
                idlePropRenderer.enabled = true;
            if (propRenderer != null)
            {
                propRenderer.sprite = null;
                propRenderer.transform.SetParent(visualRoot, false);
                propRenderer.transform.localPosition = Vector3.zero;
                propRenderer.transform.localRotation = Quaternion.identity;
                propRenderer.transform.localScale = Vector3.one;
            }
            if (_gameOverDefeatRenderer != null)
            {
                _gameOverDefeatRenderer.enabled = false;
                _gameOverDefeatRenderer.color = Color.white;
                _gameOverDefeatRenderer.transform.localPosition = Vector3.zero;
                _gameOverDefeatRenderer.transform.localRotation = Quaternion.identity;
                _gameOverDefeatRenderer.transform.localScale = Vector3.one * gameOverDefeatScale;
            }
        }

        public void ConfigureGameOverDefeat(Sprite sprite)
        {
            gameOverDefeatSprite = sprite;
            if (_gameOverDefeatRenderer == null)
            {
                var defeatObject = new GameObject("GameOverDefeat");
                defeatObject.transform.SetParent(transform, false);
                _gameOverDefeatRenderer = defeatObject.AddComponent<SpriteRenderer>();
                _gameOverDefeatRenderer.sortingOrder = 40;
                _gameOverDefeatRenderer.enabled = false;
            }

            _gameOverDefeatRenderer.sprite = gameOverDefeatSprite;
            _gameOverDefeatRenderer.transform.localScale = Vector3.one * gameOverDefeatScale;
        }

        public void StopPlayback()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            ResetPose();
        }

        public IEnumerator PlayFood(FoodCardDefinition food)
        {
            if (food == null || food.consumeSprite == null || food.consumePose == ConsumePose.None)
                yield break;

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(ConsumeRoutine(food));
            yield return _routine;
            _routine = null;
        }

        IEnumerator ConsumeRoutine(FoodCardDefinition food)
        {
            ResetPose();
            if (idlePropRenderer != null)
                idlePropRenderer.enabled = false;
            propRenderer.sprite = food.consumeSprite;
            if (mouth != null) mouth.sprite = mouthEat;

            Vector2 leftHold;
            Vector2 rightHold;
            Vector3 propPosition;
            Vector3 propScale;
            float propRotation;

            switch (food.consumePose)
            {
                case ConsumePose.Bowl:
                    SetRaisedHands(true, true);
                    leftHold = bowlLeftTarget;
                    rightHold = bowlRightTarget;
                    propPosition = new Vector3(bowlPropPosition.x, bowlPropPosition.y, 0f);
                    propScale = Vector3.one * bowlPropScale;
                    propRotation = 0f;
                    break;
                case ConsumePose.Drink:
                    SetRaisedHands(false, true);
                    leftHold = leftRestTarget;
                    rightHold = drinkRightTarget;
                    propPosition = new Vector3(drinkPropPosition.x, drinkPropPosition.y, 0f);
                    propScale = Vector3.one * drinkPropScale;
                    propRotation = drinkPropRotation;
                    break;
                default:
                    SetRaisedHands(true, true);
                    leftHold = handheldLeftTarget;
                    rightHold = handheldRightTarget;
                    propPosition = new Vector3(handheldPropPosition.x, handheldPropPosition.y, 0f);
                    propScale = Vector3.one * handheldPropScale;
                    propRotation = handheldPropRotation;
                    break;
            }

            propScale *= Mathf.Max(0.01f, propScaleMultiplier);

            propRenderer.transform.SetParent(visualRoot, false);
            Vector3 propStart = propPosition + new Vector3(propEnterOffset.x, propEnterOffset.y, 0f);
            propRenderer.transform.localPosition = propStart;
            propRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, propRotation);
            propRenderer.transform.localScale = propScale * 0.62f;

            yield return MoveProp(propStart, propPosition, propScale * 0.62f, propScale, Duration(0.34f));
            yield return MoveTargets(leftRestTarget, rightRestTarget, leftHold, rightHold, Duration(0.42f));
            yield return Bounce(0.16f, Duration(0.10f));
            if (mouth != null) mouth.sprite = mouthChew;
            if (leftEye != null) leftEye.sprite = leftEyeClosed;
            if (rightEye != null) rightEye.sprite = rightEyeClosed;
            if (food.consumedSprite != null)
                propRenderer.sprite = food.consumedSprite;
            yield return Chew(Duration(0.52f));
            if (mouth != null) mouth.sprite = mouthHappy;
            yield return Bounce(0.12f, Duration(0.12f));
            yield return MoveTargets(leftHold, rightHold, leftRestTarget, rightRestTarget, Duration(0.36f));
            ResetPose();
            yield return new WaitForSecondsRealtime(Duration(0.12f));
        }

        public IEnumerator PlayPenaltyReaction()
        {
            ResetPose();
            if (mouth != null) mouth.sprite = mouthFail;
            float elapsed = 0f;
            const float duration = 0.72f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float shake = Mathf.Sin(elapsed * 34f) * 0.055f * (1f - elapsed / duration);
                if (headBone != null)
                    headBone.localPosition = _headStart + Vector3.right * shake;
                yield return null;
            }
            ResetPose();
        }

        public IEnumerator PlayRewardReaction()
        {
            ResetPose();
            if (mouth != null) mouth.sprite = mouthHappy;
            if (leftEye != null) leftEye.sprite = leftEyeClosed;
            if (rightEye != null) rightEye.sprite = rightEyeClosed;
            yield return Bounce(0.18f, 0.22f);
            yield return Bounce(0.12f, 0.18f);
            ResetPose();
        }

        public IEnumerator PlayGameOverDefeat()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(GameOverDefeatRoutine());
            yield return _routine;
            _routine = null;
        }

        IEnumerator GameOverDefeatRoutine()
        {
            ResetPose();
            float elapsed = 0f;
            const float swellDuration = 0.34f;
            while (elapsed < swellDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / swellDuration));
                if (visualRoot != null)
                {
                    visualRoot.localScale = Vector3.Scale(
                        _visualRootStartScale,
                        new Vector3(Mathf.Lerp(1f, 1.28f, t), Mathf.Lerp(1f, 0.88f, t), 1f));
                }
                yield return null;
            }

            if (gameOverDefeatSprite == null || _gameOverDefeatRenderer == null)
            {
                if (bodyBone != null)
                    bodyBone.localScale = Vector3.Scale(_bodyStartScale, new Vector3(1.28f, 1.06f, 1f));
                if (leftEye != null) leftEye.sprite = leftEyeClosed;
                if (rightEye != null) rightEye.sprite = rightEyeClosed;
                if (mouth != null) mouth.sprite = mouthFail;
                yield return SobFallback(1.8f);
                yield break;
            }

            visualRoot.gameObject.SetActive(false);
            _gameOverDefeatRenderer.sprite = gameOverDefeatSprite;
            _gameOverDefeatRenderer.enabled = true;
            _gameOverDefeatRenderer.color = new Color(1f, 1f, 1f, 0f);
            _gameOverDefeatRenderer.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            _gameOverDefeatRenderer.transform.localScale = Vector3.one * (gameOverDefeatScale * 0.74f);

            elapsed = 0f;
            const float popDuration = 0.42f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / popDuration));
                _gameOverDefeatRenderer.color = new Color(1f, 1f, 1f, t);
                _gameOverDefeatRenderer.transform.localScale = Vector3.one * Mathf.Lerp(gameOverDefeatScale * 0.74f, gameOverDefeatScale * 1.09f, t);
                yield return null;
            }

            elapsed = 0f;
            const float settleDuration = 0.18f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settleDuration));
                _gameOverDefeatRenderer.transform.localScale = Vector3.one * Mathf.Lerp(gameOverDefeatScale * 1.09f, gameOverDefeatScale, t);
                yield return null;
            }

            elapsed = 0f;
            const float sobDuration = 1.8f;
            while (elapsed < sobDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(elapsed / sobDuration);
                float wave = Mathf.Sin(elapsed * 11f);
                _gameOverDefeatRenderer.transform.localPosition = new Vector3(
                    wave * 0.055f * fade,
                    -0.02f + Mathf.Abs(wave) * 0.035f,
                    0f);
                _gameOverDefeatRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, wave * 1.8f * fade);
                yield return null;
            }

            _gameOverDefeatRenderer.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            _gameOverDefeatRenderer.transform.localRotation = Quaternion.identity;
            _gameOverDefeatRenderer.transform.localScale = Vector3.one * gameOverDefeatScale;
        }

        float Duration(float baseDuration)
        {
            return baseDuration * Mathf.Max(0.05f, animationDurationMultiplier);
        }

        IEnumerator SobFallback(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(elapsed * 12f);
                if (bodyBone != null)
                    bodyBone.localPosition = _bodyStart + new Vector3(wave * 0.04f, Mathf.Abs(wave) * 0.025f, 0f);
                yield return null;
            }
        }

        IEnumerator MoveProp(Vector3 fromPosition, Vector3 toPosition, Vector3 fromScale, Vector3 toScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float arc = Mathf.Sin(t * Mathf.PI) * 0.18f;
                propRenderer.transform.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, t) + Vector3.up * arc;
                propRenderer.transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
                yield return null;
            }
            propRenderer.transform.localPosition = toPosition;
            propRenderer.transform.localScale = toScale;
        }

        void SetRaisedHands(bool leftRaised, bool rightRaised)
        {
            _leftHandIsRaised = leftRaised;
            _rightHandIsRaised = rightRaised;
            if (leftHand != null)
            {
                leftHand.sprite = leftRaised && leftHandRaised != null ? leftHandRaised : leftHandRelaxed;
                leftHand.transform.localRotation = _leftHandRestRotation;
            }
            if (rightHand != null)
            {
                rightHand.sprite = rightRaised && rightHandRaised != null ? rightHandRaised : rightHandRelaxed;
                rightHand.transform.localRotation = _rightHandRestRotation;
            }
        }

        IEnumerator MoveTargets(Vector2 leftFrom, Vector2 rightFrom, Vector2 leftTo, Vector2 rightTo, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _leftTarget = Vector2.LerpUnclamped(leftFrom, leftTo, t);
                _rightTarget = Vector2.LerpUnclamped(rightFrom, rightTo, t);
                yield return null;
            }
            _leftTarget = leftTo;
            _rightTarget = rightTo;
        }

        IEnumerator Chew(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = Mathf.Sin(elapsed * 26f) * 0.022f;
                headBone.localPosition = _headStart + Vector3.up * pulse;
                yield return null;
            }
            headBone.localPosition = _headStart;
        }

        IEnumerator Bounce(float height, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                bodyBone.localPosition = _bodyStart + Vector3.up * (Mathf.Sin(t * Mathf.PI) * height);
                yield return null;
            }
            bodyBone.localPosition = _bodyStart;
        }

        void SolveArm(Transform shoulder, Transform elbow, Vector2 localTarget, bool bendLeft)
        {
            if (shoulder == null || elbow == null || visualRoot == null)
                return;

            Vector3 shoulderPosition = shoulder.position;
            Vector3 targetPosition = visualRoot.TransformPoint(localTarget);
            Vector2 delta = targetPosition - shoulderPosition;
            if (singleSegmentArms)
            {
                if (delta.sqrMagnitude < 0.0001f)
                    return;
                shoulder.rotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f);
                return;
            }

            float distance = Mathf.Clamp(delta.magnitude, 0.05f, upperArmLength + forearmLength - 0.01f);
            Vector2 direction = delta.normalized;
            float along = (upperArmLength * upperArmLength - forearmLength * forearmLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperArmLength * upperArmLength - along * along));
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (bendLeft ? -1f : 1f);
            Vector2 elbowPosition = (Vector2)shoulderPosition + direction * along + perpendicular * height;

            Vector2 upperDirection = elbowPosition - (Vector2)shoulderPosition;
            shoulder.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(upperDirection.y, upperDirection.x) * Mathf.Rad2Deg + 90f);
            elbow.localPosition = Vector3.down * upperArmLength;

            Vector2 forearmDirection = (Vector2)targetPosition - (Vector2)elbow.position;
            elbow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forearmDirection.y, forearmDirection.x) * Mathf.Rad2Deg + 90f);
        }
    }
}
