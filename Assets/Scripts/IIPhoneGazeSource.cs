using UnityEngine;

/// <summary>Common gaze sample contract for direct UDP and the VTube Studio desktop API.</summary>
public interface IIPhoneGazeSource
{
    Vector2 EyeFeature { get; }
    bool FaceFound { get; }
    bool HasFreshSample { get; }
    int Sequence { get; }
    string Status { get; }
    void Poll();
    void StopReceiving();
}
