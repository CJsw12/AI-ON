using System;

// Deterministic one-dimensional cushioning model shared by story and practice runs.
public static class PhysicsImpactRules
{
    public static bool TryEvaluate(float massKg, float speedMps, float cushionMeters,
        float stopDistanceMeters, float maxForceNewtons, out float averageForceNewtons)
    {
        averageForceNewtons = 0;
        if (!IsFinite(massKg) || !IsFinite(speedMps) || !IsFinite(cushionMeters) ||
            !IsFinite(stopDistanceMeters) || !IsFinite(maxForceNewtons) ||
            massKg <= 0 || speedMps <= 0 || cushionMeters <= 0 ||
            stopDistanceMeters <= 0 || maxForceNewtons <= 0)
            return false;

        // Uniform deceleration from v₀ to rest gives t = 2d/v₀, then Favg = Δp/t.
        double force = (double)massKg * speedMps * speedMps / (2.0 * cushionMeters);
        if (double.IsNaN(force) || double.IsInfinity(force) || force > float.MaxValue)
            return false;

        averageForceNewtons = (float)force;
        return cushionMeters <= stopDistanceMeters && averageForceNewtons <= maxForceNewtons;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
