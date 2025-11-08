using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class NetworkUIRelay : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Join Code UI")]
    [SerializeField] private TMP_InputField joinCodeInput; // where client types code
    [SerializeField] private TMP_Text joinCodeLabel;       // where host sees the code

    bool started = false;

    void Awake()
    {
        hostButton.onClick.AddListener(() => Run(HostAsync));
        joinButton.onClick.AddListener(() => Run(JoinAsync));
    }

    // UnityEvents can't await, so wrap with a Task runner
    async void Run(Func<Task> task)
    {
        if (started) return;
        started = true;
        try { await task(); }
        finally { started = false; }
    }

    async Task HostAsync()
    {
        string code = await RelayUtility.StartRelayHostAsync(10);
        if (joinCodeLabel) joinCodeLabel.text = code;
        Debug.Log($"Relay host ready. JoinCode = {code}");
        ToggleUI(false);
    }

    async Task JoinAsync()
    {
        string code = joinCodeInput ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Enter a Join Code first.");
            return;
        }
        await RelayUtility.JoinRelayClientAsync(code);
        ToggleUI(false);
    }

    void ToggleUI(bool on)
    {
        if (hostButton) hostButton.interactable = on;
        if (joinButton) joinButton.interactable = on;
        if (joinCodeInput) joinCodeInput.interactable = on;
    }
}