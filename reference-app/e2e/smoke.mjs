// Optional browser smoke test (not part of scripts/test.sh). Needs a running app with S2Q_TEST_MODE=true and a Chrome/Chromium binary.
// Usage: cd e2e && npm install && BASE_URL=http://127.0.0.1:8080 CHROME_PATH=/usr/bin/google-chrome node smoke.mjs
import { chromium } from 'playwright-core';
const exe = process.env.CHROME_PATH; // path to a Chromium/Chrome binary
const browser = await chromium.launch({ executablePath: exe, args: ['--no-sandbox'] });
const base = process.env.BASE_URL ?? 'http://127.0.0.1:8080'; // app must run with S2Q_TEST_MODE=true
await fetch(base + '/api/v1/test/reset', { method: 'POST' });
const t = (page, id) => page.getByTestId(id);
async function login(page, u) {
  await page.goto(base + '/login');
  await t(page, 'login-username').fill(u); await t(page, 'login-password').fill('S2Q-Demo-2026!'); await t(page, 'login-submit').click();
  await page.waitForURL('**/incidents');
}
const results = [];
const ok = (name, cond) => { results.push([name, !!cond]); console.log(cond ? 'PASS' : 'FAIL', name); };

const ctx1 = await browser.newContext(); const rep = await ctx1.newPage();
await login(rep, 'reporter1');
ok('reporter sees new-incident nav', await t(rep, 'nav-new-incident').isVisible());
await t(rep, 'nav-new-incident').click();
await t(rep, 'new-submit').click();
ok('client validation shows error-title', await t(rep, 'error-title').isVisible());
await t(rep, 'new-title').fill('Gateway timeouts'); await t(rep, 'new-service').selectOption('1'); await t(rep, 'new-severity').selectOption('SEV1');
await t(rep, 'new-submit').click();
await rep.waitForURL('**/incidents/1');
ok('detail shows number', (await t(rep, 'detail-number').textContent()) === 'INC-0001');
ok('reporter token in localStorage', await rep.evaluate(() => !!localStorage.getItem('s2q.token')));
ok('reporter sees no audit list', (await t(rep, 'audit-list').count()) === 0);
ok('reporter sees edit button (own, open)', await t(rep, 'action-edit').isVisible());

const ctx2 = await browser.newContext(); const cmd = await ctx2.newPage();
await login(cmd, 'cmd.platform');
await cmd.goto(base + '/incidents/1');
await t(cmd, 'action-acknowledge').waitFor();
ok('commander sees acknowledge', await t(cmd, 'action-acknowledge').isVisible());
await t(cmd, 'action-acknowledge').click();
await cmd.waitForFunction(() => document.querySelector('[data-testid="detail-status"]')?.textContent === 'ACKNOWLEDGED');
ok('toast after ack', await t(cmd, 'toast').isVisible());
ok('assignee is commander', (await t(cmd, 'detail-assignee').textContent()) === 'Casey Platform');
await t(cmd, 'action-mitigate').click();
await t(cmd, 'dialog-confirm').click();
await t(cmd, 'dialog-error').waitFor();
ok('dialog-error on empty message', await t(cmd, 'dialog-error').isVisible());
await t(cmd, 'dialog-input').fill('Restarted the gateway pods'); await t(cmd, 'dialog-confirm').click();
await cmd.waitForFunction(() => document.querySelector('[data-testid="detail-status"]')?.textContent === 'MITIGATED');
await t(cmd, 'action-resolve').click(); await t(cmd, 'dialog-input').fill('Root cause was a bad deploy; rolled back'); await t(cmd, 'dialog-confirm').click();
await cmd.waitForFunction(() => document.querySelector('[data-testid="detail-status"]')?.textContent === 'RESOLVED');
ok('resolution summary shown', await t(cmd, 'detail-resolution-summary').isVisible());
await t(cmd, 'action-close').click();
await cmd.waitForTimeout(500);
ok('close blocked by ACTION_ITEM_REQUIRED (SEV1) shown in toast', (await t(cmd, 'toast').textContent()).includes('action item'));
await t(cmd, 'ai-title-input').fill('Add canary deploys'); await t(cmd, 'ai-owner-select').selectOption('5'); await t(cmd, 'ai-due-input').fill('2999-01-01'); await t(cmd, 'ai-add').click();
await cmd.waitForSelector('[data-testid="ai-item"]');
await t(cmd, 'ai-toggle').click();
await cmd.waitForFunction(() => document.querySelector('[data-testid="ai-status"]')?.textContent === 'DONE');
await t(cmd, 'action-close').click();
await cmd.waitForFunction(() => document.querySelector('[data-testid="detail-status"]')?.textContent === 'CLOSED');
ok('closed: no action buttons', (await cmd.locator('[data-testid^="action-"]').count()) === 0);
const actions = await t(cmd, 'audit-action').allTextContents();
ok('audit trail order', actions.join(',') === 'INCIDENT_CREATED,ASSIGNED,ACKNOWLEDGED,MITIGATED,RESOLVED,ACTION_ITEM_CREATED,ACTION_ITEM_UPDATED,CLOSED');
await cmd.goto(base + '/dashboard');
await t(cmd, 'dash-total-count').waitFor();
ok('dashboard total 1, closed 1', (await t(cmd, 'dash-total-count').textContent()) === '1' && (await t(cmd, 'dash-status-CLOSED').textContent()) === '1');
ok('dash-mtta numeric', /^\d+\.\d$/.test(await t(cmd, 'dash-mtta').textContent()));
await cmd.goto(base + '/incidents');
ok('list row present', (await cmd.locator('[data-testid="incident-row"][data-incident-id="1"]').count()) === 1);
ok('pagination info', (await t(cmd, 'pagination-info').textContent()) === 'Page 1 of 1');
await cmd.goto(base + '/nope/nope');
ok('not-found-page', await t(cmd, 'not-found-page').isVisible());
await t(cmd, 'logout-button').click();
await cmd.waitForURL('**/login');
ok('logout clears token', await cmd.evaluate(() => !localStorage.getItem('s2q.token')));

const ctx3 = await browser.newContext(); const v = await ctx3.newPage();
await login(v, 'viewer1');
ok('viewer has no new-incident nav', (await t(v, 'nav-new-incident').count()) === 0);
await v.goto(base + '/incidents/new'); await v.waitForURL('**/incidents');
ok('viewer redirected from /incidents/new', v.url().endsWith('/incidents'));
await browser.close();
const failed = results.filter(r => !r[1]);
console.log(`\n${results.length - failed.length}/${results.length} passed`);
process.exit(failed.length ? 1 : 0);
