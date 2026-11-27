#if UNITY_5_3_OR_NEWER
using UnityEngine;

// Practice-only: no story slot references this game.
public sealed class StretchPractice : MiniGame
{
    StretchPoseSource source;
    GameObject host;
    readonly Texture2D[] pictures = new Texture2D[4];
    static readonly string[] Names = { "양팔 위로 뻗기", "옆구리 · 화면 오른쪽으로", "옆구리 · 화면 왼쪽으로", "가슴 열기" };
    readonly int who;
    int stage, passed;
    float last, stageAt, held;
    int phase, device; // 0 demonstration, 1 following, 2 result
    float attemptAt;
    bool manual, modeChosen;
    readonly Vector2[] model = new Vector2[33];
    float leanAngle;
    int dragging = -1;
    static readonly int[] ArmJoints = { 13, 14, 15, 16 };
    void ResetModel()
    {
        leanAngle=0; dragging=-1;
        model[11]=new Vector2(600,380); model[12]=new Vector2(760,380);
        model[13]=new Vector2(570,480); model[14]=new Vector2(790,480);
        model[15]=new Vector2(550,580); model[16]=new Vector2(810,580);
        model[23]=new Vector2(630,580); model[24]=new Vector2(730,580);
    }
    void Tilt(float amount)
    {
        float next=Mathf.Clamp(leanAngle+amount,-25,25);
        float delta=(next-leanAngle)*Mathf.Deg2Rad;
        Vector2 pivot=(model[23]+model[24])*.5f;
        for(int i=11;i<=16;i++)
        {
            Vector2 v=model[i]-pivot;
            model[i]=pivot+new Vector2(v.x*Mathf.Cos(delta)-v.y*Mathf.Sin(delta),
                v.x*Mathf.Sin(delta)+v.y*Mathf.Cos(delta));
        }
        leanAngle=next;
    }
    void MoveJoint(int i, Vector2 target)
    {
        int shoulder=i%2==1 ? 11:12, elbow=shoulder+2, wrist=shoulder+4;
        if(i==elbow)
        {
            Vector2 delta=Vector2.ClampMagnitude(target-model[shoulder],110f)+model[shoulder]-model[elbow];
            model[elbow]+=delta; model[wrist]+=delta;
        }
        else model[wrist]=model[elbow]+Vector2.ClampMagnitude(target-model[elbow],110f);
    }
    void Bone(Vector2 a,Vector2 b)
    {
        float length=Vector2.Distance(a,b);
        for(int i=0;i<=Mathf.CeilToInt(length/5);i++)
        {
            Vector2 at=Vector2.Lerp(a,b,i/Mathf.Max(1f,Mathf.Ceil(length/5)));
            NovelUI.Fill(new Rect(at.x-4,at.y-4,8,8),NovelUI.Mint,4);
        }
    }
    void DrawModel(GUIStyle body, GUIStyle button)
    {
        Rect area=new Rect(180,185,780,480);
        NovelUI.Fill(area,new Color(.08f,.08f,.1f),12);
        Event e=Event.current;
        if(e.type==EventType.MouseDown && e.button==0)
            foreach(int i in ArmJoints)
                if(Vector2.Distance(e.mousePosition,model[i])<25) {dragging=i;e.Use();break;}
        if(e.type==EventType.MouseDrag && dragging>=0)
        {
            MoveJoint(dragging,new Vector2(Mathf.Clamp(e.mousePosition.x,210,930),Mathf.Clamp(e.mousePosition.y,230,635)));
            e.Use();
        }
        if(e.type==EventType.MouseUp) dragging=-1;
        if(e.type==EventType.KeyDown)
        {
            if(e.keyCode==KeyCode.LeftArrow) {Tilt(-3);e.Use();}
            else if(e.keyCode==KeyCode.RightArrow) {Tilt(3);e.Use();}
        }
        Bone(model[11],model[12]); Bone(model[11],model[23]); Bone(model[12],model[24]);
        Bone(model[23],model[24]);
        foreach(int shoulder in new[]{11,12})
        {Bone(model[shoulder],model[shoulder+2]);Bone(model[shoulder+2],model[shoulder+4]);}
        Vector2 head=(model[11]+model[12])*.5f+new Vector2(0,-55);
        NovelUI.Fill(new Rect(head.x-25,head.y-25,50,50),NovelUI.Pink,25);
        foreach(int i in ArmJoints)
        {
            NovelUI.Fill(new Rect(model[i].x-15,model[i].y-15,30,30),NovelUI.Pink,15);
            GUI.Label(new Rect(model[i].x+17,model[i].y-15,75,30),i<15 ? "팔꿈치" : "손목",body);
        }
        GUI.Label(new Rect(200,200,730,45),"분홍 관절 드래그 · ← → 몸통 기울이기 · Enter 제출",body);
        bool submit=GUI.Button(new Rect(980,475,170,48),"자세 제출",button);
        if(e.type==EventType.KeyDown && (e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter))
        {submit=true;e.Use();}
        if(submit)
        {
            if(Check()) PoseResult(true);
            else { professorLine=RetryLines[who][stage]; cue=ProfessorName+" : "+professorLine+"\n"+cue; }
        }
        if(GUI.Button(new Rect(980,535,170,48),"처음 자세",button)) {ResetModel();cue="관절을 움직여 다시 만들어 보세요.";}
        if(GUI.Button(new Rect(980,595,170,48),"시범 다시 보기",button)) {phase=0;held=0;}
        GUI.Label(new Rect(180,680,940,70),cue,body);
        if(GUI.Button(new Rect(190,755,190,38),"자세 넘기기",button)) {PoseResult(false, true);}
    }

    string professorLine = "";
    static readonly string[][] SuccessLines = {
        new[] { "좋다. 팔이 곧게 올라갔어. 호흡은 멈추지 말고.", "그렇지. 옆구리가 늘어나는 방향을 기억해.", "반대쪽도 좋다. 양쪽 균형을 맞추는 게 중요해.", "잘했다. 어깨에 힘 빼고 천천히 내려." },
        new[] { "좋아요. 손끝이 위로 길어졌네요. 편안하게 숨 쉬어요.", "잘했어요. 옆구리가 부드럽게 늘어나는 느낌을 기억해요.", "반대쪽도 잘 따라왔어요. 양쪽 느낌을 비교해 볼까요?", "가슴이 편안하게 열렸네요. 이제 천천히 쉬어요." }
    };
    static readonly string[][] RetryLines = {
        new[] { "아직이야. 두 팔을 조금 더 위로 펴 봐.", "방향을 확인하고 조금만 기울여. 억지로 꺾지 말고.", "이번엔 반대쪽이야. 서두르지 말고 다시 해 봐.", "팔꿈치는 어깨 높이, 손은 위로. 한 번 더." },
        new[] { "괜찮아요. 양팔을 편안한 범위에서 위로 뻗어 볼까요?", "시범의 방향을 다시 봐요. 조금만 기울여도 괜찮아요.", "이번에는 반대쪽이에요. 호흡하면서 다시 해 봐요.", "팔꿈치 높이와 손의 방향을 살펴볼까요? 천천히 해요." }
    };
    string ProfessorName => who == 0 ? "서진호 교수" : "강하늘 교수";
    void PoseResult(bool success, bool skipped = false)
    {
        professorLine = success ? SuccessLines[who][stage] :
            skipped ? (who == 0 ? "여기까지 하자. 무리하지 말고, 다음에 다시 해 보자." : "오늘은 여기까지 해도 좋아요. 편안하게 쉬었다가 다시 해요.") :
            RetryLines[who][stage];
        if(success) { passed++; held=2; }
        else held=0;
        phase=2;
    }

    string cue = "편안한 범위에서 움직이세요.";
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "천천히 다시 해 보자.", "좋다. 자세를 기억해.", "잘했다. 무리하지 말고." },
        new[] { "편안하게 다시 해요.", "좋아요. 호흡을 이어 가요.", "아주 좋아요. 편안히 쉬어요." } };
    public StretchPractice(MiniGameTheme theme, int professor) : base(theme)
    {
        who = professor;
        string prefix = professor == 0 ? "ProfessorSeo" : "ProfessorKang";
        string[] suffix = { "ArmsUp", "SideStretch", "SideStretch", "ChestOpen" };
        for (int i = 0; i < 4; i++) pictures[i] = Resources.Load<Texture2D>("Stretching/" + prefix + "_" + suffix[i]);
    }
    public void Release()
    {
        if (source != null) source.Release();
        if (host != null) Object.Destroy(host);
        source = null; host = null;
    }
    void Finish()
    {
        Done = true; Delta = passed == 4 ? 2 : passed > 0 ? 1 : 0;
        Summary = $"스트레칭 {passed}/4 완료 · 연습 결과는 스토리에 반영되지 않아요";
        Release();
    }
    Vector2 Point(int i)
    {
        if(manual) return (model[i]-new Vector2(200,180))/500f;
        var p = source.Landmarks[i];
        float aspect = source.FrameHeight > 0 ? (float)source.FrameWidth / source.FrameHeight : 1f;
        return new Vector2(p.x * aspect, p.y);
    }
    float Elbow(int a, int b, int c) => Vector2.Angle(Point(a)-Point(b), Point(c)-Point(b));
    bool Check()
    {
        if (!manual && (source == null || !source.IsPoseDetected || Time.unscaledTime - source.LastFrameAt > .5f))
        { cue = source == null ? "웹캠을 시작해 주세요." : source.Status; return false; }
        int[] ids = { 11,12,13,14,15,16,23,24 };
        if(!manual) foreach (int i in ids)
        {
            var p = source.Landmarks[i];
            if (source.Visibility[i] < .4f || p.x < .02f || p.x > .98f || p.y < .02f || p.y > .98f)
            { cue = "양손과 골반까지 화면에 보이게 조금 뒤로 이동해 주세요."; return false; }
        }
        Vector2 ls=Point(11), rs=Point(12), le=Point(13), re=Point(14), lw=Point(15), rw=Point(16);
        float width = Vector2.Distance(ls,rs);
        if (width < .05f) { cue = "카메라를 정면으로 바라봐 주세요."; return false; }
        float left=Elbow(11,13,15), right=Elbow(12,14,16);
        bool ok;
        if (stage == 0)
        {
            ok = lw.y < ls.y-width*.65f && rw.y < rs.y-width*.65f && left>145 && right>145;
            cue = "두 팔을 어깨너비로 머리 위에 뻗어 주세요.";
        }
        else if (stage == 1 || stage == 2)
        {
            Vector2 shoulder=(ls+rs)*.5f, hip=(Point(23)+Point(24))*.5f;
            float signedLean = (shoulder.x-hip.x) / Mathf.Max(.1f, hip.y-shoulder.y);
            float lean = Mathf.Abs(signedLean);
            ok = (stage == 1 ? signedLean > .12f : signedLean < -.12f) && lean<.65f &&
                ((lw.y<ls.y-width*.5f && left>140) || (rw.y<rs.y-width*.5f && right>140));
            cue = "한 팔을 올리고 옆으로 가볍게 기울여 주세요. 이번에는 표시된 방향으로 기울여요.";
        }
        else
        {
            ok = Mathf.Abs(le.y-ls.y)<width*.4f && Mathf.Abs(re.y-rs.y)<width*.4f &&
                lw.y<le.y-width*.3f && rw.y<re.y-width*.3f &&
                left>60 && left<120 && right>60 && right<120;
            cue = "팔꿈치를 어깨 높이로, 손은 위로 올려 골포스트 모양을 만들어 주세요.";
        }
        if(ok) cue = who == 0 ? "좋다. 그대로, 호흡은 편하게!" : "좋아요. 천천히 호흡해요.";
        return ok;
    }
    void DrawPose(Rect area)
    {
        Texture2D image = pictures[stage];
        if (image == null) return;
        float height = Mathf.Min(area.height, area.width * image.height / image.width);
        float width = height * image.width / image.height;
        Rect target = new Rect(area.center.x-width/2,area.center.y-height/2,width,height);
        GUI.DrawTextureWithTexCoords(target,image,stage==2 ? new Rect(1,0,-1,1) : new Rect(0,0,1,1));
    }
    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (Done) { GUI.Label(new Rect(150,300,980,100),Summary,body); return; }
        float now=Now, dt=Mathf.Clamp(now-last,0,.15f);
        if(Event.current.type==EventType.Repaint) last=now;
        GUI.Label(new Rect(130,115,1020,45), $"{stage+1}/4 · {Names[stage]}", title);
        if(!modeChosen)
        {
            GUI.Label(new Rect(200,220,900,70),"연습 방법을 선택해 주세요.",title);
            if(GUI.Button(new Rect(250,360,760,80),"웹캠으로 따라 하기",button))
            {manual=false;modeChosen=true;}
            if(GUI.Button(new Rect(250,480,760,80),"마우스로 자세 만들기",button))
            {manual=true;modeChosen=true;ResetModel();}
            return;
        }
        if (phase == 0)
        {
            Rect photo = new Rect(210,175,420,520);
            if(pictures[stage]!=null) DrawPose(photo);
            else GUI.Label(photo,"시범 이미지 로드 실패",body);
            GUI.Label(new Rect(665,225,440,75), who == 0 ? "먼저 내 자세를 봐. 무리하지 말고 따라 해 보자." : "먼저 자세를 살펴봐요. 편안하게 호흡해요.",body);
            GUI.Label(new Rect(665,325,440,110), stage==0 ? "양팔을 머리 위로 뻗어요." : (stage==1 || stage==2) ? "한 팔을 올리고 반대쪽 옆구리를 늘려요. 이번에는 표시된 방향으로 기울여요." : "팔꿈치를 어깨 높이로 올리고 손은 위로 향해요.",body);
            if(!manual && source==null)
            {
                var devices=WebCamTexture.devices;
                GUI.Label(new Rect(665,465,430,40),devices.Length==0 ? "웹캠을 찾을 수 없습니다." : "카메라: "+devices[Mathf.Clamp(device,0,devices.Length-1)].name,body);
                if(devices.Length>1 && GUI.Button(new Rect(665,515,210,45),"다른 카메라",button)) device=(device+1)%devices.Length;
            }
            if(GUI.Button(new Rect(665,595,300,65),"이제 따라 하기",button))
            {
                if(!manual && source==null)
                {
                    host=new GameObject("StretchPosePractice");
                    source=host.AddComponent<StretchPoseSource>();
                    source.DeviceIndex=device;
                }
                if(manual) ResetModel();
                phase=1; stageAt=attemptAt=now; held=0;
            }
            return;
        }
        if(phase==2)
        {
            if(pictures[stage]!=null) DrawPose(new Rect(170,180,350,480));
            GUI.Label(new Rect(610,280,500,100),ProfessorName+"\n“"+professorLine+"”",body);
            if(GUI.Button(new Rect(650,500,300,65),stage==3 ? "연습 결과 보기" : "다음 자세 보기",button))
            {
                stage++; held=0;
                if(stage>=4) {stage=3;Finish();}
                else phase=0;
            }
            return;
        }
        if(manual) {
            if(pictures[stage]!=null) DrawPose(new Rect(990,190,150,230));
            DrawModel(body,button); return;
        }
        Rect camera=new Rect(180,185,780,480);
        NovelUI.Fill(camera,new Color(.08f,.08f,.1f),12);
        if(source!=null && source.CameraTexture!=null)
            GUI.DrawTexture(camera,source.CameraTexture,ScaleMode.ScaleToFit);
        if(pictures[stage]!=null) DrawPose(new Rect(990,190,150,230));
        bool correct=Check();
        if(source!=null && !source.IsPoseDetected && now-attemptAt<5) stageAt=now;
        if(now-stageAt<3) cue="준비 "+Mathf.CeilToInt(3-(now-stageAt))+" · "+cue;
        else if(Event.current.type==EventType.Repaint)
        {
            held=correct ? held+dt : Mathf.Max(0,held-dt);
            if(held>=2) PoseResult(true);
        }
        GUI.Label(new Rect(180,670,940,55),cue,body);
        NovelUI.Fill(new Rect(180,735,650,20),NovelUI.Blush,10);
        NovelUI.Fill(new Rect(180,735,650*Mathf.Clamp01(held/2),20),NovelUI.Mint,10);
        GUI.Label(new Rect(860,727,260,40),$"유지 {held:F1}/2초",body);
        if(GUI.Button(new Rect(980,470,170,48),"시범 다시 보기",button)) {phase=0;held=0;}
        if(GUI.Button(new Rect(980,535,170,48),"카메라 재연결",button)) {Release();phase=0;held=0;}
        if(GUI.Button(new Rect(980,600,170,48),"자세 넘기기",button)) {PoseResult(false, true);}
        if(now-attemptAt>=30 && phase==1) PoseResult(false);
    }
}
#endif
