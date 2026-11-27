# 작업 지시서: 수어 인식 학습 PC 사전 준비

작성 2026-11-27 · 요청 home-claude-opus55(사용자 승인) → 담당: 학습 PC 에이전트(sh-com-claude, RTX 5060 Ti PC) · 상태: **요청됨**

근거 문서: [수어 인식 학습 가이드](Sign-Recognition-Training-Guide.md)(특히 1.7, 2.3~2.6, 4장), [계획서](Sign-Fingerspelling-Plan.md) 6.2·11.6절, [TTS 제작 기술 문서](TTS-Voice-Production.md) 3절(같은 GPU 사용 기록)

## 1. 목적
- 이 PC에서 수어 지문자 관절 MLP를 **GPU로 학습**할 수 있게 환경을 준비합니다.
- 학습 스크립트가 GPU에서 실제로 도는지 **합성 데이터로 확인**하고, **GPU 대 CPU 실측표**를 만듭니다.
- 실제 참여자 녹화 데이터는 아직 없습니다. 이번 작업에서는 다루지 않습니다.

## 2. 배경 (확인된 사실)
- 학습 스크립트(`Tools/SignTrain/` 아래)는 home PC의 Codex 워커가 만들고 있습니다. 구성은 `requirements.txt`, `dataset.py`, `sign_vote.py`, `train_mlp.py`, `export_onnx.py`, 테스트입니다.
  - home PC에는 NVIDIA GPU가 없어서 **CPU 경로만 검증**됩니다. **GPU 경로(`--device cuda`)는 이 PC에서 처음 확인합니다.**
- 이 PC의 GPU는 RTX 5060 Ti(Blackwell, 컴퓨트 능력 12.0)입니다. PyTorch는 **CUDA 12.8 이상 빌드(cu128·cu130)**가 필요하고, TTS 작업에서 둘 다 동작이 확인됐습니다.
- TTS에 쓴 Python 환경(3.13, 3.10 CosyVoice)과 **섞지 않습니다.** 수어 학습용 가상 환경을 따로 만듭니다.

## 3. 작업 단계

### 단계 0. 시작 확인
```powershell
git pull
bash Tools/agent-sync.sh status
Test-Path Tools/SignTrain/train_mlp.py   # True면 4·5단계까지, False면 1~3단계만 하고 보고
```
- **경로 선점:** 3단계에서 저장소 파일을 고칠 때만 선점합니다(`bash Tools/agent-sync.sh claim "수어 학습 PC 준비" Tools/SignTrain/ docs/plans/Sign-Training-PC-Setup-Request.md docs/plans/Sign-Recognition-Training-Guide.md`). 환경 설치만 하면 선점하지 않습니다.

### 단계 1. GPU·드라이버 확인
```powershell
nvidia-smi
```
- 기록: GPU 이름, 드라이버 버전, CUDA Version, VRAM

### 단계 2. Python 3.11 가상 환경 (저장소 밖)
```powershell
py -3.11 --version          # 없으면: winget install --id Python.Python.3.11 -e --scope user
py -3.11 -m venv $env:USERPROFILE\SignTrainVenv
& $env:USERPROFILE\SignTrainVenv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128
```
- `Tools/SignTrain/requirements.txt`가 있으면 그 파일 맨 위 주석의 GPU 설치 명령을 따르고, 나머지 패키지는 `pip install -r Tools/SignTrain/requirements.txt`로 설치합니다.
- 없으면 `pip install numpy scikit-learn matplotlib onnx onnxruntime`로 설치합니다.
- **설치 창(GUI)이 뜨거나 관리자 권한이 필요하면 우회하지 말고 멈춘 뒤 사용자에게 알립니다.** 학교·관리 PC라면 보안 정책을 지킵니다.

### 단계 3. GPU 인식과 간단 성능 확인
```powershell
python -c "import torch; print(torch.__version__, torch.version.cuda, torch.cuda.is_available(), torch.cuda.get_device_name(0))"
```
- `True`와 GPU 이름이 나와야 합니다.
- 간단 성능 확인: 행렬 곱 4096×4096을 bf16과 fp32로 각각 50회 실행합니다. GPU와 CPU의 걸린 시간을 기록합니다(워밍업 1회 제외, `torch.cuda.synchronize()` 포함).
- 오류가 나면 학습 가이드 7장 표(`no kernel image`, `out of memory` 등)대로 해결하고, 해결 과정을 기록합니다.

### 단계 4. 학습 스크립트 GPU 검증 (스크립트가 있을 때)
```powershell
python -m unittest discover Tools/SignTrain        # 전체 통과 확인 (CPU 기준 테스트)
```
- 테스트가 쓰는 합성 데이터 생성기로 학습 데이터를 만듭니다. 생성 방법은 스크립트 안내를 따릅니다.
- **같은 데이터·같은 설정으로 GPU와 CPU를 각각 실행합니다.** 예(실제 옵션은 스크립트 `--help`를 따름):
  ```powershell
  python Tools/SignTrain/train_mlp.py --data <합성 데이터 폴더> --sealed <봉인 가명> --device cuda --seeds 1 2 3 --time-report
  python Tools/SignTrain/train_mlp.py --data <합성 데이터 폴더> --sealed <봉인 가명> --device cpu  --seeds 1 2 3 --time-report
  ```
- 내보내기 확인: `python Tools/SignTrain/export_onnx.py ...`로 ONNX를 만들고, onnxruntime 출력이 PyTorch와 일치하는지 확인합니다.
- **GPU에서만 생기는 오류**(장치 불일치, bf16 관련, 결정성 차이 등)가 있으면 원인과 고친 내용을 기록합니다. 스크립트를 고쳤다면 CPU 테스트(`unittest`)도 다시 통과시킵니다.

### 단계 5. 실측표 작성
아래 표를 이 문서 7절 "결과" 아래에 채웁니다. 모르는 값은 "미측정"으로 적습니다.

| 항목 | GPU (RTX 5060 Ti) | CPU (Ryzen 5 5600) |
|---|---|---|
| 행렬 곱 4096² × 50회 (fp32 / bf16) | | |
| MLP 학습 총시간 (합성 데이터, 시드 3개) | | |
| 에폭당 시간 | | |
| 최대 메모리 (VRAM / RAM) | | |
| 교차 검증 평균 재현율 (두 장치가 같아야 정상) | | |
| 측정 조건 | 데이터 크기, 에폭, 배치, 정밀도, 워밍업 | |

- 관절 MLP는 입력이 작아 **GPU가 더 느릴 수도 있습니다.** 결과를 그대로 적고, 이유(데이터 전송 비용 등)를 한 줄로 설명합니다. 발표에서는 실측값만 씁니다.

### 단계 6. 실제 데이터 받을 준비 (설명만, 데이터는 아직 없음)
- 녹화본은 **게임 저장소(Git)로 받지 않습니다.** 암호화 USB 등으로 받아 `Tools/SignData/raw/<가명>/<세션>.csv`에 둡니다(Git 제외 확인: `git check-ignore -v Tools/SignData/raw/x.csv`). 근거는 학습 가이드 2.6절입니다.
- 학습 결과 폴더 `Tools/SignTrain/runs/`와 체크포인트 `*.pt`도 Git 제외인지 확인합니다.
- 이 PC의 백업·삭제 담당자, 보관 위치는 팀 결정 항목입니다. 사용자에게 확인할 항목으로 보고에 적습니다.

## 4. 하지 말 것
- 실제 참여자 데이터를 다루지 않습니다(아직 동의·녹화 전).
- 참여자 데이터, 체크포인트, 학습 결과 폴더, 가상 환경을 커밋하지 않습니다.
- TTS용 Python 환경과 `Tools/Voice/`는 바꾸지 않습니다.
- 게임 코드(`Assets/**`)는 바꾸지 않습니다. ONNX를 게임에서 실행하는 코드는 이번 범위가 아닙니다.
- 보안 정책 우회, `AION_SKIP_*`·`ALLOW_*` 우회 변수 사용을 하지 않습니다.

## 5. 커밋 (저장소 파일을 고쳤을 때만)
- 고칠 수 있는 파일: 이 문서의 상태와 7절 결과, `Tools/SignTrain/` 스크립트(GPU 버그 수정 시), 학습 가이드의 GPU 관련 문장
```powershell
Get-ChildItem Tests/*.ps1 | ForEach-Object { powershell.exe -NoProfile -ExecutionPolicy Bypass -File $_.FullName; "$($_.Name): $LASTEXITCODE" }   # 전부 0
python -m unittest discover Tools/SignTrain   # 전부 통과
git diff --check
bash Tools/agent-sync.sh finish "수어 학습 PC 준비: GPU 검증과 실측표" <고친 경로들>
```
- `git add -A` 금지. `PROGRESS.md` Done에 한 줄을 **추가**합니다(기존 줄 수정 금지).

## 6. 완료 체크리스트
- [ ] 단계 1: GPU·드라이버·CUDA 기록
- [ ] 단계 2: `SignTrainVenv`(Python 3.11)와 PyTorch cu128 설치
- [ ] 단계 3: `torch.cuda.is_available() == True`, 행렬 곱 GPU 대 CPU 시간
- [ ] 단계 4: unittest 통과, `train_mlp.py --device cuda` 성공, ONNX 일치 (스크립트가 있을 때)
- [ ] 단계 5: 실측표 작성
- [ ] 단계 6: Git 제외 확인, 사용자 확인 항목 정리
- [ ] 이 문서 상태를 "완료"(또는 "1~3단계 완료, 스크립트 대기")로 변경
- [ ] 사용자에게 보고: 설치 버전, 실측표, GPU에서만 생긴 문제와 해결, 미검증 항목, 사용자가 정할 것

## 7. 결과 (학습 PC 에이전트가 작성)
(비어 있음)
