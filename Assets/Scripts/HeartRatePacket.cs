using System;
using System.Globalization;
using System.Text;

/// <summary>Parses a single-line Wear OS heart-rate UDP JSON packet without external JSON libraries.</summary>
public struct HeartRatePacket
{
    public const int MaxLength = 512;
    public const int SupportedVersion = 1;
    public const double MinBpm = 30.0;
    public const double MaxBpm = 230.0;
    public const string ReasonBadJson = "bad_json";
    public const string ReasonBadVersion = "bad_version";
    public const string ReasonBadBpmRange = "bad_bpm_range";
    public const string ReasonBadFields = "bad_fields";
    public const string ReasonTooLong = "too_long";
    public const string ReasonOther = "other";
    const int MaxNestingDepth = 16;

    public long Seq;
    public long WatchTime;
    public double Bpm;
    public bool Ok;

    public static bool TryParse(string json, out HeartRatePacket packet)
    {
        string ignoredReason;
        return TryParse(json, out packet, out ignoredReason);
    }

    public static bool TryParse(string json, out HeartRatePacket packet, out string reason)
    {
        packet = new HeartRatePacket();
        packet.Bpm = double.NaN;
        reason = null;

        if (string.IsNullOrEmpty(json))
        {
            reason = ReasonBadJson;
            return false;
        }
        if (json.Length > MaxLength)
        {
            reason = ReasonTooLong;
            return false;
        }

        int pos = 0;
        SkipWhitespace(json, ref pos);
        if (pos >= json.Length || json[pos] != '{')
        {
            reason = ReasonBadJson;
            return false;
        }
        pos++;

        bool hasV = false;
        long version = 0;
        bool invalidV = false;
        bool hasK = false;
        bool hasSeq = false;
        long seq = 0;
        bool hasT = false;
        long watchTime = 0;
        bool hasOk = false;
        bool ok = false;
        bool hasBpm = false;
        double bpm = double.NaN;
        bool bpmOverflow = false;
        bool invalidField = false;

        SkipWhitespace(json, ref pos);
        if (pos < json.Length && json[pos] == '}')
        {
            pos++;
            SkipWhitespace(json, ref pos);
            reason = pos == json.Length ? ReasonBadFields : ReasonBadJson;
            return false;
        }

        while (true)
        {
            string key;
            if (!ReadString(json, ref pos, out key))
            {
                reason = ReasonBadJson;
                return false;
            }

            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != ':')
            {
                reason = ReasonBadJson;
                return false;
            }
            pos++;
            SkipWhitespace(json, ref pos);

            if (key == "v")
            {
                int valStart = pos;
                if (ReadInt64(json, ref pos, out version))
                {
                    hasV = true;
                }
                else
                {
                    pos = valStart;
                    if (!SkipValue(json, ref pos, 0))
                    {
                        reason = ReasonBadJson;
                        return false;
                    }
                    invalidV = true;
                }
            }
            else if (key == "k")
            {
                if (!SkipValue(json, ref pos, 0))
                {
                    reason = ReasonBadJson;
                    return false;
                }
                hasK = true;
            }
            else if (key == "seq")
            {
                int valStart = pos;
                if (ReadInt64(json, ref pos, out seq))
                {
                    hasSeq = true;
                }
                else
                {
                    pos = valStart;
                    if (!SkipValue(json, ref pos, 0))
                    {
                        reason = ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "t")
            {
                int valStart = pos;
                if (ReadInt64(json, ref pos, out watchTime))
                {
                    hasT = true;
                }
                else
                {
                    pos = valStart;
                    if (!SkipValue(json, ref pos, 0))
                    {
                        reason = ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "ok")
            {
                int valStart = pos;
                if (ReadBool(json, ref pos, out ok))
                {
                    hasOk = true;
                }
                else
                {
                    pos = valStart;
                    if (!SkipValue(json, ref pos, 0))
                    {
                        reason = ReasonBadJson;
                        return false;
                    }
                    invalidField = true;
                }
            }
            else if (key == "bpm")
            {
                int valStart = pos;
                if (ReadFiniteDouble(json, ref pos, out bpm))
                {
                    hasBpm = true;
                }
                else
                {
                    pos = valStart;
                    int numStart, numLen;
                    bool isInt;
                    if (ScanNumber(json, ref pos, out numStart, out numLen, out isInt))
                    {
                        bpmOverflow = true;
                    }
                    else
                    {
                        pos = valStart;
                        if (!SkipValue(json, ref pos, 0))
                        {
                            reason = ReasonBadJson;
                            return false;
                        }
                        invalidField = true;
                    }
                }
            }
            else
            {
                if (!SkipValue(json, ref pos, 0))
                {
                    reason = ReasonBadJson;
                    return false;
                }
            }

            SkipWhitespace(json, ref pos);
            if (pos >= json.Length)
            {
                reason = ReasonBadJson;
                return false;
            }

            char sep = json[pos];
            if (sep == ',')
            {
                pos++;
                SkipWhitespace(json, ref pos);
                continue;
            }
            if (sep == '}')
            {
                pos++;
                break;
            }
            reason = ReasonBadJson;
            return false;
        }

        SkipWhitespace(json, ref pos);
        if (pos != json.Length)
        {
            reason = ReasonBadJson;
            return false;
        }

        if (invalidV || (hasV && version != SupportedVersion))
        {
            reason = ReasonBadVersion;
            return false;
        }

        if (hasK || !hasV || !hasSeq || !hasT || !hasOk || invalidField)
        {
            reason = ReasonBadFields;
            return false;
        }

        if (bpmOverflow)
        {
            reason = ReasonBadBpmRange;
            return false;
        }

        if (ok)
        {
            if (!hasBpm)
            {
                reason = ReasonBadFields;
                return false;
            }
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm < MinBpm || bpm > MaxBpm)
            {
                reason = ReasonBadBpmRange;
                return false;
            }
        }

        packet.Seq = seq;
        packet.WatchTime = watchTime;
        packet.Bpm = hasBpm ? bpm : double.NaN;
        packet.Ok = ok;
        return true;
    }

    internal static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            char c = s[pos];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                pos++;
            }
            else
            {
                break;
            }
        }
    }

    internal static bool ReadString(string s, ref int pos, out string value)
    {
        value = null;
        if (pos >= s.Length || s[pos] != '"')
        {
            return false;
        }
        pos++;
        int start = pos;
        StringBuilder sb = null;

        while (pos < s.Length)
        {
            char c = s[pos];
            if (c == '"')
            {
                if (sb == null)
                {
                    value = s.Substring(start, pos - start);
                }
                else
                {
                    value = sb.ToString();
                }
                pos++;
                return true;
            }
            if (c < 0x20)
            {
                return false;
            }
            if (c == '\\')
            {
                if (sb == null)
                {
                    sb = new StringBuilder(s.Substring(start, pos - start));
                }
                pos++;
                if (pos >= s.Length)
                {
                    return false;
                }
                char esc = s[pos];
                switch (esc)
                {
                    case '"': sb.Append('"'); pos++; break;
                    case '\\': sb.Append('\\'); pos++; break;
                    case '/': sb.Append('/'); pos++; break;
                    case 'b': sb.Append('\b'); pos++; break;
                    case 'f': sb.Append('\f'); pos++; break;
                    case 'n': sb.Append('\n'); pos++; break;
                    case 'r': sb.Append('\r'); pos++; break;
                    case 't': sb.Append('\t'); pos++; break;
                    case 'u':
                        if (pos + 4 >= s.Length)
                        {
                            return false;
                        }
                        int codeUnit;
                        if (!TryParseHex4(s, pos + 1, out codeUnit))
                        {
                            return false;
                        }
                        sb.Append((char)codeUnit);
                        pos += 5;
                        break;
                    default:
                        return false;
                }
            }
            else
            {
                if (sb != null)
                {
                    sb.Append(c);
                }
                pos++;
            }
        }
        return false;
    }

    static bool TryParseHex4(string s, int start, out int result)
    {
        result = 0;
        for (int i = 0; i < 4; i++)
        {
            char c = s[start + i];
            int digit;
            if (c >= '0' && c <= '9') digit = c - '0';
            else if (c >= 'a' && c <= 'f') digit = 10 + (c - 'a');
            else if (c >= 'A' && c <= 'F') digit = 10 + (c - 'A');
            else return false;
            result = (result << 4) | digit;
        }
        return true;
    }

    static bool ReadBool(string s, ref int pos, out bool value)
    {
        value = false;
        if (MatchLiteral(s, ref pos, "true"))
        {
            value = true;
            return true;
        }
        if (MatchLiteral(s, ref pos, "false"))
        {
            value = false;
            return true;
        }
        return false;
    }

    static bool MatchLiteral(string s, ref int pos, string literal)
    {
        int len = literal.Length;
        if (pos + len > s.Length)
        {
            return false;
        }
        for (int i = 0; i < len; i++)
        {
            if (s[pos + i] != literal[i])
            {
                return false;
            }
        }
        pos += len;
        return true;
    }

    static bool ScanNumber(string s, ref int pos, out int start, out int length, out bool isInteger)
    {
        start = pos;
        length = 0;
        isInteger = true;

        if (pos >= s.Length)
        {
            return false;
        }
        if (s[pos] == '-')
        {
            pos++;
            if (pos >= s.Length)
            {
                return false;
            }
        }

        if (s[pos] == '0')
        {
            pos++;
            if (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            {
                return false;
            }
        }
        else if (s[pos] >= '1' && s[pos] <= '9')
        {
            pos++;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            {
                pos++;
            }
        }
        else
        {
            return false;
        }

        if (pos < s.Length && s[pos] == '.')
        {
            isInteger = false;
            pos++;
            int fracStart = pos;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            {
                pos++;
            }
            if (pos == fracStart)
            {
                return false;
            }
        }

        if (pos < s.Length && (s[pos] == 'e' || s[pos] == 'E'))
        {
            isInteger = false;
            pos++;
            if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            {
                pos++;
            }
            int expStart = pos;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            {
                pos++;
            }
            if (pos == expStart)
            {
                return false;
            }
        }

        length = pos - start;
        return length > 0;
    }

    internal static bool ReadInt64(string s, ref int pos, out long value)
    {
        value = 0;
        int start, length;
        bool isInteger;
        if (!ScanNumber(s, ref pos, out start, out length, out isInteger) || !isInteger)
        {
            return false;
        }
        string token = s.Substring(start, length);
        return long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    internal static bool ReadFiniteDouble(string s, ref int pos, out double value)
    {
        value = double.NaN;
        int start, length;
        bool isInteger;
        if (!ScanNumber(s, ref pos, out start, out length, out isInteger))
        {
            return false;
        }
        string token = s.Substring(start, length);
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    internal static bool SkipValue(string s, ref int pos, int depth)
    {
        if (depth > MaxNestingDepth || pos >= s.Length)
        {
            return false;
        }

        char c = s[pos];
        if (c == '"')
        {
            string ignored;
            return ReadString(s, ref pos, out ignored);
        }
        if (c == '{')
        {
            pos++;
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}')
            {
                pos++;
                return true;
            }
            while (true)
            {
                string ignoredKey;
                if (!ReadString(s, ref pos, out ignoredKey))
                {
                    return false;
                }
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != ':')
                {
                    return false;
                }
                pos++;
                SkipWhitespace(s, ref pos);
                if (!SkipValue(s, ref pos, depth + 1))
                {
                    return false;
                }
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length)
                {
                    return false;
                }
                if (s[pos] == ',')
                {
                    pos++;
                    SkipWhitespace(s, ref pos);
                    continue;
                }
                if (s[pos] == '}')
                {
                    pos++;
                    return true;
                }
                return false;
            }
        }
        if (c == '[')
        {
            pos++;
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']')
            {
                pos++;
                return true;
            }
            while (true)
            {
                if (!SkipValue(s, ref pos, depth + 1))
                {
                    return false;
                }
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length)
                {
                    return false;
                }
                if (s[pos] == ',')
                {
                    pos++;
                    SkipWhitespace(s, ref pos);
                    continue;
                }
                if (s[pos] == ']')
                {
                    pos++;
                    return true;
                }
                return false;
            }
        }
        if (c == 't')
        {
            return MatchLiteral(s, ref pos, "true");
        }
        if (c == 'f')
        {
            return MatchLiteral(s, ref pos, "false");
        }
        if (c == 'n')
        {
            return MatchLiteral(s, ref pos, "null");
        }
        if (c == '-' || (c >= '0' && c <= '9'))
        {
            int start, length;
            bool isInteger;
            return ScanNumber(s, ref pos, out start, out length, out isInteger);
        }
        return false;
    }
}
