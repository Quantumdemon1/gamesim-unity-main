using UnityEngine;

namespace Gamesim.House
{
    /// <summary>A reachable room destination used by prototype validation.</summary>
    public sealed class HouseRoomMarker : MonoBehaviour
    {
        [SerializeField] private string roomName;
        public string RoomName => roomName;
        public void Configure(string value) => roomName = value;
    }
}
