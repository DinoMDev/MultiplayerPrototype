using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public struct InputCmd : INetworkSerializable
{
    public int Tick;
    public float H, V;
    public float YawDelta;
    public bool Jump;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Tick);
        s.SerializeValue(ref H);
        s.SerializeValue(ref V);
        s.SerializeValue(ref YawDelta);
        s.SerializeValue(ref Jump);
    }
}

public struct StateSnap : INetworkSerializable
{
    public int Tick;
    public Vector3 Pos;
    public Vector3 Vel;
    public float Yaw;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Tick);
        s.SerializeValue(ref Pos);
        s.SerializeValue(ref Vel);
        s.SerializeValue(ref Yaw);
    }
}

[RequireComponent(typeof(CharacterController))]
public class PlayerPrediction : NetworkBehaviour
{
    [Header("Owner input")]
    [SerializeField] private float mouseYawSensitivity = 180f; // deg/sec per Mouse X = 1

    [Header("Remote interpolation")]
    [SerializeField] private float interpLerp = 12f;   // higher = snappier
    [SerializeField] private float snapThreshold = 0.15f; // reconcile pos threshold (m)
    [SerializeField] private float snapYawThreshold = 3f;  // reconcile yaw threshold (deg)

    private CharacterController cc;

    // sim state (always reflects current transform)
    private SimState state;

    // buffers for prediction
    private readonly Dictionary<int, InputCmd> pendingInputs = new();
    private readonly Dictionary<int, SimState> predicted     = new();

    // tick bookkeeping
    private int lastSimulatedTick = -1;  // last tick we simulated locally
    private int lastSentTick      = -1;  // last input tick sent to server
    private float fixedDt;               // 1 / NetworkConfig.TickRate

    // server guard (what server processed last)
    private int lastProcessedServerTick = -1;

    // interpolation targets for non-owners
    private Vector3 targetPos;
    private float   targetYaw;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        state.Position = transform.position;
        state.Yaw      = transform.rotation.eulerAngles.y;
        state.Velocity = Vector3.zero;

        targetPos = state.Position;
        targetYaw = state.Yaw;

        fixedDt = 1f / NetworkManager.Singleton.NetworkConfig.TickRate;
    }

    private void Update()
    {
        int tick = (int)NetworkManager.Singleton.LocalTime.Tick;

        if (IsOwner)
        {
            // Simulate exactly once per network tick (catch up if needed)
            for (int t = lastSimulatedTick + 1; t <= tick; t++)
            {
                var cmd = ReadInput(t);

                // store pre-step state
                predicted[t] = state;

                // apply locally (prediction)
                ApplyCmd(ref state, in cmd, fixedDt);

                // send exactly once per tick
                if (t > lastSentTick)
                {
                    SubmitInputServerRpc(cmd);
                    lastSentTick = t;
                }
            }

            lastSimulatedTick = tick;

            // ensure transform follows predicted state
            cc.transform.position = state.Position;
            cc.transform.rotation = Quaternion.Euler(0, state.Yaw, 0);
        }
        else
        {
            // non-owner: smooth interpolation
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, targetPos, interpLerp * dt);
            float yaw = Mathf.LerpAngle(transform.rotation.eulerAngles.y, targetYaw, interpLerp * dt);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
    }

    private InputCmd ReadInput(int tick)
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float yawDelta = Input.GetAxis("Mouse X") * mouseYawSensitivity * Time.deltaTime;
        bool jump = Input.GetButton("Jump");

        var cmd = new InputCmd { Tick = tick, H = h, V = v, YawDelta = yawDelta, Jump = jump };
        pendingInputs[tick] = cmd;
        return cmd;
    }

    private void ApplyCmd(ref SimState s, in InputCmd cmd, float dt)
    {
        // set current transform from state before stepping
        cc.transform.position = s.Position;
        cc.transform.rotation = Quaternion.Euler(0, s.Yaw, 0);

        PlayerMotor.Step(ref s,
            new SimInput { Horizontal = cmd.H, Vertical = cmd.V, YawDelta = cmd.YawDelta, Jump = cmd.Jump },
            dt, cc);

        // write back to transform
        cc.transform.position = s.Position;
        cc.transform.rotation = Quaternion.Euler(0, s.Yaw, 0);
    }

    // ====== SERVER ======

    [ServerRpc]
    private void SubmitInputServerRpc(InputCmd cmd, ServerRpcParams rpc = default)
    {
        // Drop dupes / out-of-order same-tick inputs
        if (cmd.Tick <= lastProcessedServerTick) return;
        lastProcessedServerTick = cmd.Tick;

        float dt = fixedDt;

        // simulate on the authoritative instance of THIS player
        var po = NetworkManager.Singleton.ConnectedClients[rpc.Receive.SenderClientId].PlayerObject;
        var pred = po.GetComponent<PlayerPrediction>();

        pred.cc.transform.position = pred.state.Position;
        pred.cc.transform.rotation = Quaternion.Euler(0, pred.state.Yaw, 0);

        pred.ApplyCmd(ref pred.state, in cmd, dt);

        // send authoritative snapshot back
        var snap = new StateSnap
        {
            Tick = cmd.Tick,
            Pos  = pred.state.Position,
            Vel  = pred.state.Velocity,
            Yaw  = pred.state.Yaw
        };
        pred.ReceiveSnapshotClientRpc(snap);
    }

    // ====== CLIENT ======

    [ClientRpc]
    private void ReceiveSnapshotClientRpc(StateSnap snap, ClientRpcParams rp = default)
    {
        if (!IsOwner)
        {
            // non-owner receives new target
            targetPos = snap.Pos;
            targetYaw = snap.Yaw;
            return;
        }

        // owner: reconcile
        if (!predicted.TryGetValue(snap.Tick, out var predictedState))
            return;

        bool needPos = Vector3.Distance(predictedState.Position, snap.Pos) > snapThreshold;
        bool needYaw = Mathf.Abs(Mathf.DeltaAngle(predictedState.Yaw, snap.Yaw)) > snapYawThreshold;

        if (needPos || needYaw)
        {
            // rewind to server state
            state = new SimState { Position = snap.Pos, Velocity = snap.Vel, Yaw = snap.Yaw };

            // replay inputs from next tick to current
            int currentTick = (int)NetworkManager.Singleton.LocalTime.Tick;
            for (int t = snap.Tick + 1; t <= currentTick; t++)
            {
                if (pendingInputs.TryGetValue(t, out var cmd))
                    ApplyCmd(ref state, in cmd, fixedDt);
            }

            cc.transform.position = state.Position;
            cc.transform.rotation = Quaternion.Euler(0, state.Yaw, 0);
        }

        // cleanup old buffers
        predicted.Remove(snap.Tick);
        pendingInputs.Remove(snap.Tick);
    }
}