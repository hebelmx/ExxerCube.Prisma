// Phase-2 evidence: screenshot the live Prisma Web UI at http://localhost:5172
// Run with: node screenshot.mjs
import { chromium } from 'playwright';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BASE = 'http://localhost:5172';
const OUT = __dirname;

const SCREENS = [
  { name: 'home',              url: '/',                        wait: 'networkidle' },
  { name: 'login',             url: '/Account/Login',           wait: 'networkidle' },
  { name: 'dashboard',         url: '/dashboard',               wait: 'networkidle' },
  { name: 'processing',        url: '/processing',              wait: 'networkidle' },
  { name: 'manual-review',     url: '/review',                  wait: 'networkidle' },
  { name: 'export',            url: '/export',                  wait: 'networkidle' },
  { name: 'audit',             url: '/audit',                   wait: 'networkidle' },
  { name: 'sla-dashboard',     url: '/sla',                     wait: 'networkidle' },
  { name: 'health',            url: '/health',                  wait: 'domcontentloaded' },
];

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const page = await context.newPage();

const results = [];

for (const screen of SCREENS) {
  const url = BASE + screen.url;
  let status = 'unknown';
  let httpStatus = null;
  try {
    const response = await page.goto(url, { waitUntil: screen.wait, timeout: 20000 });
    httpStatus = response?.status();
    await page.waitForTimeout(2000); // let Blazor render
    const file = path.join(OUT, `screen-${screen.name}.png`);
    await page.screenshot({ path: file, fullPage: true });
    status = 'captured';
    results.push({ screen: screen.name, url, httpStatus, file: `screen-${screen.name}.png`, status });
  } catch (e) {
    results.push({ screen: screen.name, url, httpStatus, status: 'error', error: e.message });
  }
}

await browser.close();

console.log('\n=== Screenshot Results ===');
for (const r of results) {
  if (r.status === 'captured') {
    console.log(`OK  [${r.httpStatus}] ${r.screen} → ${r.file}`);
  } else {
    console.log(`ERR [${r.httpStatus ?? '?'}] ${r.screen} → ${r.error}`);
  }
}
