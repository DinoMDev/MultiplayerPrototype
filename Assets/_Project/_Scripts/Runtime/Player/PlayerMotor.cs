using UnityEngine;

public struct SimState
{
    public Vector3 Position;
    public Vector3 Velocity;   // y used for gravity/jump
    public float Yaw;          // degrees
}

public struct SimInput
{
    public float Horizontal;
    public float Vertical;
    public float YawDelta;     // deg this tick
    public bool Jump;
}

public static class PlayerMotor
{
    // Tune these
    public const float MoveSpeed = 6f;
    public const float Gravity   = -9.81f;
    public const float JumpVel   = 5.8f;
    public const float Friction  = 10f; // small damping for stability

    // Single deterministic sim step
    public static void Step(ref SimState s, in SimInput i, float dt, CharacterController cc)
    {
        // rotate yaw only
        s.Yaw += i.YawDelta;
        if (s.Yaw >= 360f) s.Yaw -= 360f;
        if (s.Yaw <    0f) s.Yaw += 360f;

        // forward/right from yaw (no pitch/roll)
        var rot = Quaternion.Euler(0f, s.Yaw, 0f);
        var fwd = rot * Vector3.forward;
        var right = rot * Vector3.right;

        // desired planar velocity
        Vector3 wish = right * i.Horizontal + fwd * i.Vertical;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        Vector3 planar = wish * MoveSpeed;

        // gravity + jump
        s.Velocity.y += Gravity * dt;
        if (cc.isGrounded)
        {
            s.Velocity.y = Mathf.Max(s.Velocity.y, -2f);
            if (i.Jump) s.Velocity.y = JumpVel;
        }

        // compose delta, move, capture resulting position
        Vector3 delta = new Vector3(planar.x, s.Velocity.y, planar.z) * dt;
        cc.Move(delta);
        s.Position = cc.transform.position;

        // light damping
        s.Velocity *= Mathf.Clamp01(1f - Friction * dt);
    }
}