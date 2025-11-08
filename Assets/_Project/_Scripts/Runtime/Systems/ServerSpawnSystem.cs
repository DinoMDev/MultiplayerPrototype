using UnityEngine;
using Unity.Netcode;

public class ServerSpawnSystem : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    private int nextIndex = 0;

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        var playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null) return;

        var t = spawnPoints[nextIndex % spawnPoints.Length];
        nextIndex++;

        var cc = playerObject.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        playerObject.transform.SetPositionAndRotation(t.position, t.rotation);

        if (cc) cc.enabled = true;
    }
}
