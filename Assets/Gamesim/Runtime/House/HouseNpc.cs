using UnityEngine;

namespace Gamesim.House
{
    /// <summary>Authored identity for a house character. Simulation state is added in a later slice.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class HouseNpc : MonoBehaviour
    {
        [SerializeField] private string id = "maya";
        [SerializeField] private string displayName = "Maya";

        private TextMesh nameLabel;
        private Camera labelCamera;
        private Vector3 labelRestPosition;
        private bool labelPositionKnown;

        public string Id => id;
        public string DisplayName => displayName;

        public void Configure(string stableId, string characterName)
        {
            id = string.IsNullOrWhiteSpace(stableId) ? "maya" : stableId.Trim();
            displayName = string.IsNullOrWhiteSpace(characterName) ? "Maya" : characterName.Trim();
            RefreshNameLabel();
        }

        private void OnEnable()
        {
            RefreshNameLabel();
            labelCamera = Camera.main;
        }

        private void OnTransformChildrenChanged()
        {
            RefreshNameLabel();
        }

        private void RefreshNameLabel()
        {
            if (nameLabel == null)
            {
                nameLabel = GetComponentInChildren<TextMesh>(true);
            }

            if (nameLabel != null)
            {
                if(!labelPositionKnown){labelRestPosition=nameLabel.transform.localPosition;labelPositionKnown=true;}
                nameLabel.text = displayName;
            }
        }

        private void LateUpdate()
        {
            if (nameLabel == null)
            {
                return;
            }

            var seat=GetComponent<HouseSeatPresentation>();
            if(seat!=null && seat.Active)nameLabel.transform.position=seat.VisualFocus+Vector3.up*.35f;
            else if(labelPositionKnown)nameLabel.transform.localPosition=labelRestPosition;

            if (labelCamera == null || !labelCamera.isActiveAndEnabled)
            {
                labelCamera = Camera.main;
            }

            if (labelCamera != null)
            {
                // TextMesh faces its local -Z axis, matching a camera-facing billboard.
                nameLabel.transform.rotation = labelCamera.transform.rotation;
                // Close-range only (camera Phase 4): a name over every head from across the house is
                // a screen of labels; at a conversation's distance it is who you are talking to.
                float away = Vector3.Distance(labelCamera.transform.position, nameLabel.transform.position);
                NameTagAlpha = Mathf.Clamp01((NameTagFar - away) / Mathf.Max(0.01f, NameTagFar - NameTagNear));
                // A tag is sized for the dollhouse's distances; a director's shot comes within a few
                // metres, where the same tag would fill the top of the frame. Inside the near
                // distance it shrinks with the range instead, down to a floor that still reads.
                if (!nameTagScaleKnown) { nameTagScale = nameLabel.transform.localScale; nameTagScaleKnown = true; }
                nameLabel.transform.localScale = nameTagScale * Mathf.Clamp(away / NameTagNear, NameTagNearestScale, 1f);
                var colour = nameLabel.color;
                if (!Mathf.Approximately(colour.a, NameTagAlpha)) { colour.a = NameTagAlpha; nameLabel.color = colour; }
            }
        }

        /// <summary>Inside this distance the name tag is fully shown; beyond <see cref="NameTagFar"/> it is gone.</summary>
        public const float NameTagNear = 9f, NameTagFar = 14f;
        /// <summary>How small a tag gets, as a share of its authored size, when a shot is right on top of it.</summary>
        public const float NameTagNearestScale = 0.3f;
        private Vector3 nameTagScale;
        private bool nameTagScaleKnown;
        /// <summary>How much of the name tag the camera's distance leaves visible, 0 to 1.</summary>
        public float NameTagAlpha { get; private set; } = 1f;
    }
}
