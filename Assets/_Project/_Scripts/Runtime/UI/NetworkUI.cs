using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    //Buttons for hosting, joining, and starting the game
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;

    private void Awake()
        {
            hostButton.onClick.AddListener(() =>
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("Host started");
            });

            clientButton.onClick.AddListener(() =>
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("Client started");
            });
            serverButton.onClick.AddListener(() =>
            {
                NetworkManager.Singleton.StartServer();
                Debug.Log("Server started");
            });
        }
}
