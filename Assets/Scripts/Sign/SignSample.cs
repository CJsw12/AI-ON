using System;
using System.Globalization;
using System.Text;
public sealed class SignSample
{
    public string Person, Session, Trial, Label, Handedness;
    public long Frame, TimestampMs;
    public int Width, Height;
    public float HandednessScore;
    public float[] Landmarks = new float[63];
    public const string Header = "person,session,trial,label,frame,timestamp_ms,width,height,handedness,handedness_score,x0,y0,z0,x1,y1,z1,x2,y2,z2,x3,y3,z3,x4,y4,z4,x5,y5,z5,x6,y6,z6,x7,y7,z7,x8,y8,z8,x9,y9,z9,x10,y10,z10,x11,y11,z11,x12,y12,z12,x13,y13,z13,x14,y14,z14,x15,y15,z15,x16,y16,z16,x17,y17,z17,x18,y18,z18,x19,y19,z19,x20,y20,z20";
    static string Field(string s) { if(string.IsNullOrWhiteSpace(s)||s.IndexOfAny(new[]{',','\r','\n','"'})>=0) throw new ArgumentException("잘못된 CSV 필드"); return s; }
    public string ToCsvLine() {
        if(Landmarks==null||Landmarks.Length!=63||Width<=0||Height<=0||(Handedness!="Left"&&Handedness!="Right")||float.IsNaN(HandednessScore)||float.IsInfinity(HandednessScore)) throw new ArgumentException("잘못된 표본");
        var c=CultureInfo.InvariantCulture; var b=new StringBuilder();
        b.Append(Field(Person)).Append(',').Append(Field(Session)).Append(',').Append(Field(Trial)).Append(',').Append(Field(Label)).Append(',').Append(Frame.ToString(c)).Append(',').Append(TimestampMs.ToString(c)).Append(',').Append(Width.ToString(c)).Append(',').Append(Height.ToString(c)).Append(',').Append(Handedness).Append(',').Append(HandednessScore.ToString("R",c));
        foreach(float v in Landmarks) { if(float.IsNaN(v)||float.IsInfinity(v)) throw new ArgumentException("잘못된 좌표"); b.Append(',').Append(v.ToString("R",c)); } return b.ToString();
    }
    public static bool TryParse(string line,out SignSample s,out string error) {
        s=null; error="잘못된 CSV 줄입니다."; if(string.IsNullOrEmpty(line)) return false; var a=line.Split(','); if(a.Length!=73) return false;
        var x=new SignSample(); var c=CultureInfo.InvariantCulture;
        try { x.Person=Field(a[0]); x.Session=Field(a[1]); x.Trial=Field(a[2]); x.Label=Field(a[3]); } catch(ArgumentException) { return false; }
        if(!long.TryParse(a[4],NumberStyles.Integer,c,out x.Frame)||!long.TryParse(a[5],NumberStyles.Integer,c,out x.TimestampMs)||!int.TryParse(a[6],NumberStyles.Integer,c,out x.Width)||!int.TryParse(a[7],NumberStyles.Integer,c,out x.Height)||x.Width<=0||x.Height<=0) return false;
        x.Handedness=a[8]; if(x.Handedness!="Left"&&x.Handedness!="Right") return false;
        if(!float.TryParse(a[9],NumberStyles.Float,c,out x.HandednessScore)||float.IsNaN(x.HandednessScore)||float.IsInfinity(x.HandednessScore)) return false;
        for(int i=0;i<63;i++) if(!float.TryParse(a[i+10],NumberStyles.Float,c,out x.Landmarks[i])||float.IsNaN(x.Landmarks[i])||float.IsInfinity(x.Landmarks[i])) return false;
        s=x; error=null; return true;
    }
}
