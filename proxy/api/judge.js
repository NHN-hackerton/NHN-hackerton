const ANTHROPIC_API_URL = 'https://api.anthropic.com/v1/messages';
const ANTHROPIC_VERSION = '2023-06-01';
const MODEL = 'claude-sonnet-4-6';
const MAX_TOKENS = 1024;
const REQUEST_TIMEOUT_MS = 20000;
const ALLOWED_ORIGIN = process.env.ALLOWED_ORIGIN || '*';

function setCorsHeaders(res) {
  res.setHeader('Access-Control-Allow-Origin', ALLOWED_ORIGIN);
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
}

module.exports = async function handler(req, res) {
  setCorsHeaders(res);

  if (req.method === 'OPTIONS') {
    res.status(204).end();
    return;
  }

  if (req.method !== 'POST') {
    res.status(405).json({ error: 'POST만 허용됩니다.' });
    return;
  }

  const { systemPrompt, userMessage } = req.body || {};

  if (
    typeof systemPrompt !== 'string' || !systemPrompt.trim() ||
    typeof userMessage !== 'string' || !userMessage.trim()
  ) {
    res.status(400).json({ error: 'systemPrompt와 userMessage가 필요합니다.' });
    return;
  }

  const apiKey = process.env.ANTHROPIC_API_KEY;
  if (!apiKey) {
    res.status(500).json({ error: '서버 설정 오류' });
    return;
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  try {
    const response = await fetch(ANTHROPIC_API_URL, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'x-api-key': apiKey,
        'anthropic-version': ANTHROPIC_VERSION,
      },
      body: JSON.stringify({
        model: MODEL,
        max_tokens: MAX_TOKENS,
        system: systemPrompt,
        messages: [{ role: 'user', content: userMessage }],
      }),
      signal: controller.signal,
    });

    const data = await response.json();

    if (!response.ok) {
      res.status(response.status).json({ error: data?.error?.message || 'Claude API 오류' });
      return;
    }

    const textBlock = Array.isArray(data.content)
      ? data.content.find((block) => block.type === 'text')
      : null;

    if (!textBlock) {
      res.status(502).json({ error: 'Claude 응답에 텍스트가 없습니다.' });
      return;
    }

    res.status(200).json({ text: textBlock.text });
  } catch (err) {
    if (err.name === 'AbortError') {
      res.status(504).json({ error: '요청 시간 초과' });
      return;
    }
    res.status(500).json({ error: '프록시 처리 중 오류가 발생했습니다.' });
  } finally {
    clearTimeout(timeout);
  }
};
