# AI 모드 서버 (Cloudflare Worker)

게임의 AI 모드는 이 서버를 거쳐 Gemini를 부릅니다. Gemini API 키는 Cloudflare 비밀 설정에만 있고, 이 폴더·레포·게임 빌드 어디에도 없습니다.

- 게임 → `POST https://aion-ai-proxy.dugeun-campus.workers.dev` (`{ system, user }`, 헤더 `X-AION-Client: dugeun-campus`)
- 서버 → Gemini `gemini-3.1-flash-lite` (모델·길이 고정. `-latest` 별칭은 2026-11-05에 응답이 15~40초 걸리는 모델로 바뀌어 버전을 고정, IP당 분당 20회)
- Gemini는 일부 지역(예: 홍콩)에서 오는 호출을 "User location is not supported"로 거절합니다. 그래서 구글 호출은 미국 동부에 고정한 Durable Object(`GeminiRelay`)에서만 나갑니다.
- 응답은 Gemini의 OpenAI 호환 JSON 그대로라 게임 코드(`AiProfessor.cs`)가 직접 호출과 똑같이 읽습니다.

## 배포 (처음 한 번)

```
cd Server/ai-proxy
npx wrangler login                        # 브라우저에서 Cloudflare 로그인
npx wrangler secret put GEMINI_API_KEY    # Gemini 키 붙여넣기 (화면에 안 남음)
npx wrangler deploy                       # 나온 주소를 AiProfessor.ProxyEndpoint에 넣기
```

코드를 고친 뒤에는 `npx wrangler deploy`만 다시 하면 됩니다. 키를 바꿀 때는 `secret put`을 다시 합니다.

## 비용

Cloudflare Workers 무료 요금제(하루 요청 10만 건까지)와 Gemini 무료 등급 안에서 돌아갑니다. 무료 한도를 넘으면 돈이 나가는 게 아니라 요청이 잠시 거절되고, 게임에는 "무료 사용 한도에 걸렸어요"가 뜹니다.
