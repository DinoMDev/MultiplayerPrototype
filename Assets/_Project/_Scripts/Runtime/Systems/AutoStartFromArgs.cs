using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class AutoStartFromArgs : MonoBehaviour
{
    void Start()
    {
        #if UNITY_EDITOR
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.Exists(args, a => a == "-client"))
                NetworkManager.Singleton.StartClient();
        #endif
    }
}
