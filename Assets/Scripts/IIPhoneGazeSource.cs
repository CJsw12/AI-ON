using UnityEngine;

/// <summary>Common gaze sample contract for direct UDP and the VTube Studio desktop API.</summary>
public interface IIPhoneGazeSource
{
    Vector2 EyeFeature { get; }
    /// <summary>Head yaw/pitch in the same orientation as EyeFeature (x = yaw, y = pitch).</summary>
    Vector2 HeadFeature { get; }
    /// <summary>
    /// ARKit eye-shape gaze (x = look out/in, y = look up − down, each about −1..1); zero when the source doesn't
    /// send blendshapes. Keeps responding when looking down, where the eyelid hides the iris.
    /// </summary>
    Vector2 LookFeature { get; }
    /// <summary>0 = eyes open, 1 = closed; -1 when the source doesn't report it.</summary>
    float Blink { get; }
    bool FaceFound { get; }
    bool HasFreshSample { get; }
    int Sequence { get; }
    string Status { get; }
    void Poll();
    void StopReceiving();
}
