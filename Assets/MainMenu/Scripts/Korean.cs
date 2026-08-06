namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 조직원 이름을 문장에 끼울 때 조사를 맞춰주는 도우미.
    /// 이름이 데이터(JSON)에서 오기 때문에 "신참 조직원는"처럼 틀린 조사가 그대로 노출된다.
    /// </summary>
    public static class Korean
    {
        /// <summary>마지막 글자에 받침이 있는가. (한글이 아니면 false)</summary>
        public static bool HasFinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return false;   // 한글 음절 범위 밖
            return (c - 0xAC00) % 28 != 0;                 // 종성 인덱스가 0이면 받침 없음
        }

        /// <summary>받침 여부에 맞는 조사를 붙인다. 예: Josa("신참 조직원", "은", "는") → "신참 조직원은"</summary>
        public static string Josa(string word, string withFinal, string withoutFinal)
            => word + (HasFinalConsonant(word) ? withFinal : withoutFinal);

        public static string Eun(string w)  => Josa(w, "은", "는");
        public static string Ga(string w)   => Josa(w, "이", "가");
        public static string Eul(string w)  => Josa(w, "을", "를");
        public static string Gwa(string w)  => Josa(w, "과", "와");
        public static string Euro(string w) => Josa(w, "으로", "로");
    }
}
