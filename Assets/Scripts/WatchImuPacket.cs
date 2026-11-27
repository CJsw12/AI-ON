using System;

/// <summary>Parses a single-line Wear OS IMU UDP JSON packet ("k":"imu") without external JSON libraries.</summary>
public struct WatchImuPacket
{
    public const int MaxLength = 512;
    public const int SupportedVersion = 1;

    public long Seq;
    public long WatchTime;
    public double Ax;
    public double Ay;
    public double Az;
    public double Gx;
    public double Gy;
    public double Gz;
    public double PeakAccel;
    public double PeakGyro;
    public int Samples;

    public static bool TryParse(string json, out WatchImuPacket p)
    {
        string ignoredReason;
        return TryParse(json, out p, out ignoredReason);
    }

    public static bool TryParse(string json, out WatchImuPacket p, out string reason)
    {
        p = new WatchImuPacket();
        reason = null;

        if (string.IsNullOrEmpty(json))
        {
            reason = HeartRatePacket.ReasonBadJson;
            return false;
        }
        if (json.Length > MaxLength)
        {
            reason = HeartRatePacket.ReasonTooLong;
            return false;
        }

        int pos = 0;
        HeartRatePacket.SkipWhitespace(json, ref pos);
        if (pos >= json.Length || json[pos] != '{')
        {
            reason = HeartRatePacket.ReasonBadJson;
            return false;
        }
        pos++;

        bool hasV = false;
        long version = 0;
        bool invalidV = false;
        bool hasK = false;
        string kind = null;
        bool hasSeq = false;
        long seq = 0;
        bool hasT = false;
        long watchTime = 0;
        bool hasA = false;
        double ax = 0.0, ay = 0.0, az = 0.0;
        bool hasG = false;
        double gx = 0.0, gy = 0.0, gz = 0.0;
        bool hasAm = false;
        double am = 0.0;
        bool hasGm = false;
        double gm = 0.0;
        bool hasN = false;
        long n = 0;
        bool invalidField = false;

        HeartRatePacket.SkipWhitespace(json, ref pos);
        if (pos < json.Length && json[pos] == '}')
        {
            pos++;
            HeartRatePacket.SkipWhitespace(json, ref pos);
            reason = pos == json.Length ? HeartRatePacket.ReasonBadFields : HeartRatePacket.ReasonBadJson;
            return false;
        }

        while (true)
        {
            string key;
            if (!HeartRatePacket.ReadString(json, ref pos, out key))
            {
                reason = HeartRatePacket.ReasonBadJson;
                return false;
            }

            HeartRatePacket.SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != ':')
            {
                reason = HeartRatePacket.ReasonBadJson;
                return false;
            }
            pos++;
            HeartRatePacket.SkipWhitespace(json, ref pos);

            if (key == "v")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadInt64(json, ref pos, out version))
                {
                    hasV = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidV = true;
                }
            }
            else if (key == "k")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadString(json, ref pos, out kind))
                {
                    hasK = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "seq")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadInt64(json, ref pos, out seq))
                {
                    hasSeq = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "t")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadInt64(json, ref pos, out watchTime))
                {
                    hasT = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "a")
            {
                int valStart = pos;
                if (ReadVec3(json, ref pos, out ax, out ay, out az))
                {
                    hasA = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "g")
            {
                int valStart = pos;
                if (ReadVec3(json, ref pos, out gx, out gy, out gz))
                {
                    hasG = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "am")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadFiniteDouble(json, ref pos, out am))
                {
                    hasAm = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "gm")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadFiniteDouble(json, ref pos, out gm))
                {
                    hasGm = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "n")
            {
                int valStart = pos;
                if (HeartRatePacket.ReadInt64(json, ref pos, out n))
                {
                    hasN = true;
                }
                else
                {
                    pos = valStart;
                    if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                    {
                        reason = HeartRatePacket.ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else
            {
                if (!HeartRatePacket.SkipValue(json, ref pos, 0))
                {
                    reason = HeartRatePacket.ReasonBadJson;
                    return false;
                }
            }

            HeartRatePacket.SkipWhitespace(json, ref pos);
            if (pos >= json.Length)
            {
                reason = HeartRatePacket.ReasonBadJson;
                return false;
            }

            char sep = json[pos];
            if (sep == ',')
            {
                pos++;
                HeartRatePacket.SkipWhitespace(json, ref pos);
                continue;
            }
            if (sep == '}')
            {
                pos++;
                break;
            }
            reason = HeartRatePacket.ReasonBadJson;
            return false;
        }

        HeartRatePacket.SkipWhitespace(json, ref pos);
        if (pos != json.Length)
        {
            reason = HeartRatePacket.ReasonBadJson;
            return false;
        }

        if (invalidV || (hasV && version != SupportedVersion))
        {
            reason = HeartRatePacket.ReasonBadVersion;
            return false;
        }
        if (invalidField || !hasV || !hasK || kind != "imu" || !hasSeq || !hasT)
        {
            reason = HeartRatePacket.ReasonBadFields;
            return false;
        }
        if (!hasA || !hasG || !hasAm || !hasGm || !hasN)
        {
            reason = HeartRatePacket.ReasonBadFields;
            return false;
        }
        if (am < 0.0 || gm < 0.0 || n < 1 || n > int.MaxValue)
        {
            reason = HeartRatePacket.ReasonBadFields;
            return false;
        }

        p.Seq = seq;
        p.WatchTime = watchTime;
        p.Ax = ax;
        p.Ay = ay;
        p.Az = az;
        p.Gx = gx;
        p.Gy = gy;
        p.Gz = gz;
        p.PeakAccel = am;
        p.PeakGyro = gm;
        p.Samples = (int)n;
        return true;
    }

    static bool ReadVec3(string s, ref int pos, out double x, out double y, out double z)
    {
        x = y = z = 0.0;
        if (pos >= s.Length || s[pos] != '[')
        {
            return false;
        }
        pos++;

        HeartRatePacket.SkipWhitespace(s, ref pos);
        if (!HeartRatePacket.ReadFiniteDouble(s, ref pos, out x))
        {
            return false;
        }

        HeartRatePacket.SkipWhitespace(s, ref pos);
        if (pos >= s.Length || s[pos] != ',')
        {
            return false;
        }
        pos++;

        HeartRatePacket.SkipWhitespace(s, ref pos);
        if (!HeartRatePacket.ReadFiniteDouble(s, ref pos, out y))
        {
            return false;
        }

        HeartRatePacket.SkipWhitespace(s, ref pos);
        if (pos >= s.Length || s[pos] != ',')
        {
            return false;
        }
        pos++;

        HeartRatePacket.SkipWhitespace(s, ref pos);
        if (!HeartRatePacket.ReadFiniteDouble(s, ref pos, out z))
        {
            return false;
        }

        HeartRatePacket.SkipWhitespace(s, ref pos);
        if (pos >= s.Length || s[pos] != ']')
        {
            return false;
        }
        pos++;
        return true;
    }
}
