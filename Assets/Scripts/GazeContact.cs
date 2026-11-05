using UnityEngine;

/// <summary>
/// Decides whether the player is looking at a target (e.g. a professor's eyes) without flicker:
/// contact starts only after the gaze stays inside the small enter radius for a moment, and ends only
/// after it leaves the larger exit radius for a moment. Gaze noise near the edge therefore can't toggle it.
/// </summary>
public sealed class GazeContact
{
    readonly float enterRadius, exitRadius, enterSeconds, exitSeconds;
    float pendingSince = -1;

    public bool InContact { get; private set; }
    /// <summary>Seconds of continuous contact so far; 0 when not in contact.</summary>
    public float HeldSeconds { get; private set; }

    public GazeContact(float enterRadius = 90f, float exitRadius = 160f, float enterSeconds = .25f, float exitSeconds = .35f)
    {
        this.enterRadius = enterRadius; this.exitRadius = Mathf.Max(enterRadius, exitRadius);
        this.enterSeconds = enterSeconds; this.exitSeconds = exitSeconds;
    }

    public void Reset() { InContact = false; HeldSeconds = 0; pendingSince = -1; }

    /// <summary>Call once per frame with canvas-space gaze and target centre. Returns InContact.</summary>
    public bool Update(Vector2 gaze, Vector2 target, float now, float deltaTime)
    {
        float distance = Vector2.Distance(gaze, target);
        bool wantsToFlip = InContact ? distance > exitRadius : distance < enterRadius;
        if (!wantsToFlip) pendingSince = -1;
        else if (pendingSince < 0) pendingSince = now;
        else if (now - pendingSince >= (InContact ? exitSeconds : enterSeconds)) { InContact = !InContact; pendingSince = -1; }
        HeldSeconds = InContact ? HeldSeconds + deltaTime : 0;
        return InContact;
    }
}
