// Local mock of the DeepSeek balance API for development/testing.
// Usage: node server.js [port]
const http = require('http');

const port = parseInt(process.argv[2] || process.env.PORT || '18080', 10);
const state = {
  balance: parseFloat(process.env.START || '42.80'),
  step: parseFloat(process.env.STEP || '0.37'),
  scenario: process.env.SCENARIO || 'ok',
  hits: 0
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  if (url.pathname === '/user/balance') {
    state.hits++;
    if (state.scenario === '401') {
      res.writeHead(401, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: { message: 'Authentication Fails, Your api key is invalid', type: 'authentication_error' } }));
      return;
    }
    if (state.scenario === '500') {
      res.writeHead(500, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: { message: 'server error' } }));
      return;
    }
    if (state.scenario === 'hang') {
      return; // never respond
    }
    state.balance -= state.step;
    if (state.balance < 0) state.balance = 0;
    const body = {
      is_available: state.balance > 0,
      balance_infos: [
        {
          currency: 'CNY',
          total_balance: state.balance.toFixed(4),
          granted_balance: Math.min(30, state.balance).toFixed(4),
          topped_up_balance: Math.max(0, state.balance - 30).toFixed(4)
        }
      ]
    };
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(body));
    return;
  }
  if (url.pathname === '/__state') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(state));
    return;
  }
  if (url.pathname === '/__set') {
    if (url.searchParams.has('balance')) state.balance = parseFloat(url.searchParams.get('balance'));
    if (url.searchParams.has('step')) state.step = parseFloat(url.searchParams.get('step'));
    if (url.searchParams.has('scenario')) state.scenario = url.searchParams.get('scenario');
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(state));
    return;
  }
  res.writeHead(404, { 'Content-Type': 'application/json' });
  res.end('{"error":"not found"}');
});

server.listen(port, '127.0.0.1', () => {
  console.log('mock deepseek listening on http://127.0.0.1:' + port);
});
