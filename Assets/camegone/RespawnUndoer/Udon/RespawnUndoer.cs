
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace camegone.RespawnUndoer
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RespawnUndoer : UdonSharpBehaviour
    {
        // the last position before respawn
        public Vector3 PlayerPositionRecalled { get; private set; }
        // the last rotation before respawn
        public Quaternion PlayerRotationRecalled { get; private set; }

        [Header("Settings")]

        [Tooltip("Whether allow players to undo respawn by respawning in respawn-area.")]
        [SerializeField] private bool _allowDoubleClickRespawn = true;
        [Tooltip("Objects that move when you respawn. You can set a teleporter destination to where you were before you respawned, or you can make the teleporter an interface to undo the respawn.")]
        [SerializeField] private GameObject[] _objectsToBeMovedOnRespawn;

        private bool isInRespawnArea = true;
        private Vector3 lastPlayerPosition;
        private Quaternion lastPlayerRotation;
        void Start()
        {
            lastPlayerPosition = Networking.LocalPlayer.GetPosition();
            lastPlayerRotation = Networking.LocalPlayer.GetRotation();
            PlayerPositionRecalled = lastPlayerPosition;
            PlayerRotationRecalled = lastPlayerRotation;

            SendCustomEventDelayedSeconds(nameof(_UpdateState), 0.5f);
        }

        // non-network-callable event to track player's transform
        public void _UpdateState()
        {
            // recursion
            SendCustomEventDelayedSeconds(nameof(_UpdateState), 0.5f);
            // if the player near the respawn point, dont override the saved value
            if (isInRespawnArea)
                return;

            // Debug.Log($"[RespawnUndoer] Update positions", this);
            lastPlayerPosition = Networking.LocalPlayer.GetPosition();
            lastPlayerRotation = Networking.LocalPlayer.GetRotation();
        }

        // detect local player entering respawn area
        // (cannot use OnPlayerCollisionEnter)
        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer)
                return;

            Debug.Log($"[RespawnUndoer] Entering respawn-area", this);
            // delaying to set the flag to prevent race condition
            SendCustomEventDelayedSeconds(nameof(_SetFlag), 0.1f);
        }

        public void _SetFlag()
        {
            isInRespawnArea = true;
        }


        // detect local player leaving respawn area
        // (cannot use OnPlayerCollisionExit)
        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer)
                return;

            Debug.Log($"[RespawnUndoer] Exiting respawn-area", this);
            isInRespawnArea = false;
        }


        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer)
                return;
            if (isInRespawnArea)
            {
                if (_allowDoubleClickRespawn)
                    UndoRespawn();
            }
            else
            {
                Debug.Log($"[RespawnUndoer] Saving last-position", this);
                PlayerPositionRecalled = lastPlayerPosition;
                PlayerRotationRecalled = lastPlayerRotation;

                // this array will be valid because of Uinity's serialization
                foreach (var obj in _objectsToBeMovedOnRespawn)
                {
                    // skip invalid objects
                    if (!Utilities.IsValid(obj))
                        continue;

                    obj.transform.position = lastPlayerPosition;
                    obj.transform.rotation = lastPlayerRotation;
                }
            }
        }

        public void UndoRespawn()
        {
            Debug.Log($"[RespawnUndoer] Undoing respawn", this);
            Networking.LocalPlayer.TeleportTo(PlayerPositionRecalled, PlayerRotationRecalled);
        }
    }
}