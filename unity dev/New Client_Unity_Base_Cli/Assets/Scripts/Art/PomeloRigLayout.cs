using UnityEngine;

namespace Cabo.Client.Art
{
    /// <summary>
    /// Pomelo rig landmarks measured on the 1076 x 1461 front reference.
    /// Adjust these pixel coordinates when fine-tuning the character pose.
    /// </summary>
    public static class PomeloRigLayout
    {
        public const float PixelsPerUnit = 256f;
        public static readonly Vector2 OriginPx = new Vector2(538f, 808f);

        public static readonly Vector2 HeadCenterPx = new Vector2(538f, 308f);
        public static readonly Vector2 LeftEyeCenterPx = new Vector2(456f, 431f);
        public static readonly Vector2 RightEyeCenterPx = new Vector2(603f, 431f);
        public static readonly Vector2 MouthCenterPx = new Vector2(530f, 503f);
        public static readonly Vector2 BodyNeckPx = new Vector2(538f, 550f);

        public static readonly Vector2 LeftCuffPx = new Vector2(376f, 756f);
        public static readonly Vector2 LeftWristPx = new Vector2(286f, 906f);
        public static readonly Vector2 RightCuffPx = new Vector2(700f, 756f);
        public static readonly Vector2 RightWristPx = new Vector2(790f, 906f);

        public static readonly Vector2 LeftLegTopPx = new Vector2(452f, 1032f);
        public static readonly Vector2 RightLegTopPx = new Vector2(590f, 1032f);

        public static readonly Vector2 BodyScale = new Vector2(1.30f, 1.18f);
        public static readonly Vector2 HeadScale = new Vector2(1.195f, 1.178f);
        public static readonly Vector2 EyeScale = new Vector2(1.12f, 1.18f);
        public static readonly Vector2 MouthScale = new Vector2(1.10f, 1.10f);
        public static readonly Vector2 LegScale = Vector2.one;
        public const float ForearmScale = 1.11f;
        public const float HandScale = 1.00f;

        // Sprite-space corrections align each forearm's cuff-to-wrist axis with local down.
        public const float LeftForearmCorrectionDegrees = 17.3f;
        public const float RightForearmCorrectionDegrees = -16.0f;
        public const float LeftHandCorrectionDegrees = 31.0f;
        public const float RightHandCorrectionDegrees = -31.0f;

        public static Vector3 ToLocal(Vector2 referencePixel)
        {
            return new Vector3(
                (referencePixel.x - OriginPx.x) / PixelsPerUnit,
                (OriginPx.y - referencePixel.y) / PixelsPerUnit,
                0f);
        }

        public static Vector2 ToLocal2(Vector2 referencePixel)
        {
            var point = ToLocal(referencePixel);
            return new Vector2(point.x, point.y);
        }

        public static float ArmLength(Vector2 cuff, Vector2 wrist)
        {
            return Vector2.Distance(cuff, wrist) / PixelsPerUnit;
        }
    }
}
