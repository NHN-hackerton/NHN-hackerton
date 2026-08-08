using System;
using UnityEngine;

namespace TopDogDetective.Judge
{
    /// <summary>
    /// 프록시 접속 정보(URL·토큰)를 읽는다.
    ///
    /// [왜 환경변수만으로는 안 되는가]
    ///   WebGL 빌드는 브라우저 샌드박스 안에서 돌아가 Environment.GetEnvironmentVariable이
    ///   항상 빈 값이다. 배포판에서 실제 LLM 판정을 쓰려면 값이 빌드에 포함돼야 하므로,
    ///   Resources/ProxyConfig.json을 1순위로 읽는다.
    ///
    /// [왜 커밋하지 않는가]
    ///   토큰이 저장소에 평문으로 남으면 URL만 아는 임의 호출을 막는 의미가 없어진다.
    ///   ProxyConfig.json은 .gitignore 대상이고, 커밋되는 건 ProxyConfig.example.json뿐이다.
    ///   (빌드 산출물에는 값이 들어간다 — 프록시 토큰은 "완전한 인증"이 아니라
    ///    "URL만 아는 임의 호출 차단"용이라는 proxy/README.md의 전제 그대로다.)
    ///
    /// 우선순위: Resources/ProxyConfig.json → 환경변수(TOPDOG_PROXY_URL / TOPDOG_PROXY_TOKEN)
    /// </summary>
    public static class ProxyConfig
    {
        const string ResourceName = "ProxyConfig";
        const string UrlEnvVar    = "TOPDOG_PROXY_URL";
        const string TokenEnvVar  = "TOPDOG_PROXY_TOKEN";

        [Serializable]
        class Data
        {
            public string proxyUrl;
            public string proxyToken;
        }

        static bool loaded;
        static string cachedUrl = "";
        static string cachedToken = "";

        /// <summary>설정 파일의 URL. 없으면 빈 문자열.</summary>
        public static string Url { get { EnsureLoaded(); return cachedUrl; } }

        /// <summary>설정 파일의 토큰. 없으면 빈 문자열.</summary>
        public static string Token { get { EnsureLoaded(); return cachedToken; } }

        /// <summary>URL·토큰이 모두 갖춰졌는지. 하나라도 비면 실제 통신이 불가능하다.</summary>
        public static bool IsComplete =>
            !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Token);

        /// <summary>왜 설정이 불완전한지 사람이 읽을 한 줄. IsComplete면 null.</summary>
        public static string DescribeProblem()
        {
            if (IsComplete) return null;

            bool hasUrl   = !string.IsNullOrWhiteSpace(Url);
            bool hasToken = !string.IsNullOrWhiteSpace(Token);
            string missing = !hasUrl && !hasToken ? "URL과 토큰이 모두"
                           : !hasUrl              ? "URL이"
                                                  : "토큰이";

            return $"프록시 {missing} 비어 있습니다. " +
                   $"Assets/Resources/{ResourceName}.json을 만들고 " +
                   $"(ProxyConfig.example.json 참고) proxyUrl·proxyToken을 채우세요. " +
                   $"에디터에서는 환경변수 {UrlEnvVar} / {TokenEnvVar}로도 넘길 수 있습니다.";
        }

        /// <summary>설정 파일을 다시 읽는다. (에디터에서 값을 고친 뒤 Play 없이 반영할 때)</summary>
        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null)
            {
                try
                {
                    var data = JsonUtility.FromJson<Data>(asset.text);
                    if (data != null)
                    {
                        cachedUrl   = (data.proxyUrl   ?? "").Trim();
                        cachedToken = (data.proxyToken ?? "").Trim();
                    }
                }
                catch (Exception e)
                {
                    // 파싱 실패를 조용히 넘기면 "왜 Mock으로 도느냐"를 추적할 수 없다.
                    Debug.LogError($"[ProxyConfig] Resources/{ResourceName}.json 파싱 실패: {e.Message}");
                }
            }

            // 설정 파일이 없거나 비어 있으면 환경변수로 보완한다 (에디터 전용 경로).
            if (string.IsNullOrWhiteSpace(cachedUrl))   cachedUrl   = ReadEnv(UrlEnvVar);
            if (string.IsNullOrWhiteSpace(cachedToken)) cachedToken = ReadEnv(TokenEnvVar);
        }

        /// <summary>환경변수 읽기. 플랫폼에 따라 막혀 있으면 빈 문자열로 취급한다.</summary>
        static string ReadEnv(string key)
        {
            try { return (Environment.GetEnvironmentVariable(key) ?? "").Trim(); }
            catch { return ""; }   // WebGL 등에서는 접근이 막힌다
        }
    }
}
