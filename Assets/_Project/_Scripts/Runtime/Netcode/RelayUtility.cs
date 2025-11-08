using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Unity.Networking.Transport.Relay; 

public static class RelayUtility
{
    public static async Task<string> StartRelayHostAsync(int maxConnections = 10)
    {
        await EnsureUGS();
        Allocation a = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(a.AllocationId);

        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.SetRelayServerData(new RelayServerData(a, "dtls"));
        NetworkManager.Singleton.StartHost();
        Debug.Log($"Relay Host started. JoinCode: {joinCode}");
        return joinCode;
    }

    public static async Task JoinRelayClientAsync(string joinCode)
    {
        await EnsureUGS();
        JoinAllocation a = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.SetRelayServerData(new RelayServerData(a, "dtls"));
        NetworkManager.Singleton.StartClient();
        Debug.Log("Relay Client started");
    }

    static async Task EnsureUGS()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}