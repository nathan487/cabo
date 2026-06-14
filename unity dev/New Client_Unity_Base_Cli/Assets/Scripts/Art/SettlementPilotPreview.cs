using UnityEngine;

namespace Cabo.Client.Art
{
    public sealed class SettlementPilotPreview : MonoBehaviour
    {
        public Texture2D referenceImage;
        SettlementStageRuntime _stage;

        void Start()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("PreviewScreenCamera");
                cameraObject.tag = "MainCamera";
                var screenCamera = cameraObject.AddComponent<Camera>();
                screenCamera.clearFlags = CameraClearFlags.SolidColor;
                screenCamera.backgroundColor = new Color(0.98f, 0.92f, 0.76f, 1f);
                screenCamera.cullingMask = 0;
            }

            _stage = SettlementStageRuntime.Create(transform);
            _stage.PlayPilotPreview();
        }

        void OnGUI()
        {
            var oldColor = GUI.color;
            GUI.color = new Color(0.98f, 0.92f, 0.76f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float halfWidth = Screen.width * 0.5f;
            if (referenceImage != null)
                GUI.DrawTexture(new Rect(0f, 0f, halfWidth, Screen.height), referenceImage, ScaleMode.ScaleToFit, true);
            if (_stage != null && _stage.Output != null)
                GUI.DrawTexture(new Rect(halfWidth, 0f, Screen.width - halfWidth, Screen.height), _stage.Output, ScaleMode.ScaleToFit, true);

            GUI.color = oldColor;
        }
    }
}
