using UnityEngine;

namespace Gamesim.House
{
    /// <summary>Authored identity for a house character. Simulation state is added in a later slice.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class HouseNpc : MonoBehaviour
    {
        [SerializeField] private string id = "maya";
        [SerializeField] private string displayName = "Maya";

        private TextMesh nameLabel;
        private Camera labelCamera;

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
                nameLabel.text = displayName;
            }
        }

        private void LateUpdate()
        {
            if (nameLabel == null)
            {
                return;
            }

            if (labelCamera == null || !labelCamera.isActiveAndEnabled)
            {
                labelCamera = Camera.main;
            }

            if (labelCamera != null)
            {
                // TextMesh faces its local -Z axis, matching a camera-facing billboard.
                nameLabel.transform.rotation = labelCamera.transform.rotation;
            }
        }
    }
}
