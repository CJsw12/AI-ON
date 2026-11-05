# AI 모드 서버 (Cloudflare Worker)

게임의 AI 모드는 이 서버를 거쳐 GPT-6.1 Terra를 먼저 부르고, 실패하면 Gemini가 대신 답합니다. API 키(OpenAI·Gemini)는 Cloudflare 비밀 설정에만 있고, 이 폴더·레포·게임 빌드 어디에도 없습니다.

- 게임 → `POST https://aion-ai-proxy.dugeun-campus.workers.dev` (`{ system, user }`, 헤더 `X-AION-Client: dugeun-campus`)
- 서버 → 1순위 OpenAI `gpt-6.1-terra` (reasoning low, 12초 제한) → 실패·시간 초과·빈 응답·일일 한도 초과면 2순위 Gemini `gemini-3.1-flash-lite` (22초 제한). 두 제한을 합쳐도 게임의 40초 대기보다 짧습니다.
- Terra 일일 한도: 하루(UTC) 500회. 넘으면 그날은 Gemini만 씁니다. `wrangler.toml`의 `[vars] TERRA_DAILY_LIMIT`로 바꿀 수 있고, 0이면 Terra를 끕니다. OpenAI 키·권한·크레딧 오류(401/403/429)가 나면 10분 동안 Terra를 건너뜁니다.
- 어느 모델이 답했는지는 응답 헤더 `X-AION-Model`에 나옵니다.
- 모델·길이 고정, IP당 분당 20회. Gemini `-latest` 별칭은 2026-11-05에 응답이 15~40초 걸리는 모델로 바뀌어 버전을 고정했습니다.
- Gemini는 일부 지역(예: 홍콩)에서 오는 호출을 "User location is not supported"로 거절합니다. OpenAI도 홍콩을 지원하지 않습니다. 그래서 두 호출 모두 미국 동부에 고정한 Durable Object(`GeminiRelay`, 이름은 기존 배포와 맞추려고 유지)에서만 나갑니다.
- 응답은 OpenAI 형식 JSON 그대로라(Gemini도 OpenAI 호환 주소 사용) 게임 코드(`AiProfessor.cs`)가 직접 호출과 똑같이 읽습니다.

## 배포 (처음 한 번)

```
cd Server/ai-proxy
npx wrangler login                        # 브라우저에서 Cloudflare 로그인
npx wrangler secret put GEMINI_API_KEY    # Gemini 키 붙여넣기 (화면에 안 남음)
npx wrangler secret put OPENAI_API_KEY    # OpenAI 키 붙여넣기. 없으면 Gemini만 씁니다
npx wrangler deploy                       # 나온 주소를 AiProfessor.ProxyEndpoint에 넣기
```

코드를 고친 뒤에는 `npx wrangler deploy`만 다시 하면 됩니다. 키를 바꿀 때는 `secret put`을 다시 합니다.

## 비용

Cloudflare Workers와 Gemini는 무료 등급 안에서 돌아갑니다(한도를 넘으면 돈이 나가지 않고 요청이 잠시 거절됩니다).

Terra는 유료입니다(입력 100만 토큰당 $1, 출력 $5). 채점 1회가 약 $0.003, 최악 약 $0.01이라 하루 500회 한도면 하루 최대 약 $5입니다. 빌린 OpenAI 계정이라 계정 쪽 사용 한도를 걸 수 없으므로 이 일일 한도가 비용 상한 역할을 합니다. 크레딧이 떨어지면 자동으로 Gemini가 답합니다.
