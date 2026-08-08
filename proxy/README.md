# Top Dog Detective — LLM 프록시

## 이 폴더는 무엇인가

Unity WebGL 클라이언트가 Claude API를 안전하게 호출하기 위한 **API 키 은닉용 Vercel
서버리스 프록시**입니다. `ANTHROPIC_API_KEY`는 이 프록시(서버 환경변수)에만 존재하며,
Unity 빌드나 저장소 어디에도 포함되지 않습니다. Unity는 이 프록시의 `/api/judge`
엔드포인트만 호출하고, 프록시가 대신 Claude API를 호출해 결과를 돌려줍니다.

Unity 프로젝트(`Assets/`)와 같은 저장소의 하위 폴더일 뿐, 별도 저장소가 아닙니다
(이 폴더 안에서 `git init`을 실행하지 않습니다 — 루트의 저장소 하나만 사용).

## 구조

```
proxy/
├── api/judge.js     — 판정 요청을 Claude API로 전달하는 서버리스 함수
├── package.json
├── .env.example     — 필요한 환경변수 목록 (실제 값은 .env에 두지 않음)
└── .gitignore        — .env / .vercel / node_modules 제외
```

Unity 프로젝트(`Assets/`)와 같은 저장소에 있지만, 배포 시 Vercel의 Root Directory를
`proxy`로 지정해 이 폴더만 독립적으로 빌드·배포합니다.

## 배포 방법 (Vercel)

1. Vercel에서 이 저장소(NHN-hackerton)를 Import
2. **Root Directory**를 `proxy`로 지정 (지정하지 않으면 Unity 프로젝트 루트를 빌드하려다 실패함)
3. Project Settings → Environment Variables 에 다음을 등록
   (코드에는 하드코딩하지 않음 — `.env`는 저장소에 커밋되지 않음)
   - `ANTHROPIC_API_KEY` — Claude API 키
   - `PROXY_TOKEN` — 필수. Unity가 `X-Proxy-Token` 헤더로 보낼 임의의 문자열을 정해서
     등록. CORS(`ALLOWED_ORIGIN`)는 브라우저 간접 호출만 막을 뿐이라, URL만 알고 직접
     호출하는 임의 요청(curl, 스크립트 등)을 막으려면 이 토큰이 필요하다. 미설정 시
     모든 요청이 500으로 거부된다.
   - `ALLOWED_ORIGIN`은 선택 사항입니다. 미설정 시 모든 도메인에서 호출 가능(`*`)하며,
     배포 도메인이 확정되면 해당 값으로 좁힐 수 있습니다.
4. Deploy

> ⚠️ **환경변수를 나중에 추가/수정한 경우 반드시 재배포(Redeploy)해야 반영됩니다.**
> Vercel은 환경변수를 빌드 시점에 함수에 주입하므로, Environment Variables 화면에서
> 값을 등록·변경한 뒤 기존 배포에는 자동으로 적용되지 않습니다 — Deployments 탭에서
> 최신 배포를 Redeploy 하세요.

## 로컬 테스트

```bash
cd proxy
npm i -g vercel   # 최초 1회
cp .env.example .env   # .env에 ANTHROPIC_API_KEY·PROXY_TOKEN 값을 직접 채워넣기 (커밋 금지)
vercel dev
```

기본적으로 `http://localhost:3000/api/judge`에서 뜬다. 별도 터미널에서 호출 확인
(`.env`에 넣은 `PROXY_TOKEN`과 동일한 값을 헤더에 넣어야 401이 나지 않는다):

```bash
curl -X POST http://localhost:3000/api/judge \
  -H "Content-Type: application/json" \
  -H "X-Proxy-Token: <.env의 PROXY_TOKEN 값>" \
  -d '{"systemPrompt": "너는 신참 조직원이다.", "userMessage": "요즘 애들 중에 너만큼 야무진 놈이 없대."}'
```

`{ "text": "..." }` 형태로 응답이 오면 정상. `vercel dev`는 서버리스 함수의 `(req, res)`
시그니처를 그대로 실행하므로, `node api/judge.js`로 직접 실행하는 것은 동작하지 않는다.

## API

`POST /api/judge`

요청 헤더: `X-Proxy-Token: <PROXY_TOKEN 값>` (필수 — 없거나 틀리면 401)

요청 바디:

```json
{ "systemPrompt": "...", "userMessage": "..." }
```

성공 응답:

```json
{ "text": "..." }
```

실패 응답: `{ "error": "..." }` (4xx/5xx, 토큰 불일치는 401)

- CORS 허용 — Unity WebGL 빌드가 어느 도메인에서 호스팅되든 호출 가능 (단, 브라우저
  간접 호출만 대상 — 직접 호출은 `X-Proxy-Token`으로 막음)
- Claude API 요청 20초 타임아웃
- 모델: `claude-sonnet-4-6`

## Unity 쪽 접속 설정

Unity는 `Assets/Resources/ProxyConfig.json`에서 URL·토큰을 읽는다
(없으면 환경변수 `TOPDOG_PROXY_URL` / `TOPDOG_PROXY_TOKEN`으로 보완 — 단 **환경변수는
WebGL 빌드에서 동작하지 않는다**. 브라우저 샌드박스가 환경변수 접근을 막기 때문에,
배포판에서 실제 판정을 쓰려면 반드시 이 JSON을 채워야 한다).

```bash
cp Assets/Resources/ProxyConfig.example.json Assets/Resources/ProxyConfig.json
# proxyUrl · proxyToken을 채운다. proxyToken은 Vercel의 PROXY_TOKEN과 같은 값.
```

`ProxyConfig.json`은 `.gitignore` 대상이라 커밋되지 않는다. 커밋되는 건
`ProxyConfig.example.json` 템플릿뿐이다.

> ⚠️ `Access-Control-Allow-Headers`에 `X-Proxy-Token`이 없으면 브라우저가 preflight
> 단계에서 요청을 거부해 **WebGL 빌드는 예외 없이 실패한다**. 이 헤더를 고친 뒤에는
> 반드시 재배포해야 반영된다 — 배포된 함수는 옛 코드를 그대로 들고 있다.
