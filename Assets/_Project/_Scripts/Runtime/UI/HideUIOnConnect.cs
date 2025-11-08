using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class HideUIOnConnect : MonoBehaviour
{
    void OnEnable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += _ => gameObject.SetActive(false);
        NetworkManager.Singleton.OnClientDisconnectCallback += _ => gameObject.SetActive(true);
    }
    void OnDisable()
    {
        if(NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= _ => gameObject.SetActive(false);
        NetworkManager.Singleton.OnClientDisconnectCallback -= _ => gameObject.SetActive(true);
    }
}
