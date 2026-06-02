/**
 * Builds the full Chrome MV3 extension into `dist/`.
 *
 * Steps:
 *   1. `ng build` the Angular UI (outputHashing: none → stable main.js/styles.css).
 *   2. esbuild the service worker (background.ts → ESM) and content script
 *      (content.ts → IIFE).
 *   3. Assemble `dist/`: Angular bundle + the three entry HTMLs + manifest +
 *      icons + the two esbuild bundles.
 *
 * Run via `npm run build:ext`.
 */
import { build as esbuild } from 'esbuild';
import { execFileSync } from 'node:child_process';
import {
  cpSync,
  existsSync,
  mkdirSync,
  readdirSync,
  rmSync,
  statSync,
} from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');
const dist = join(root, 'dist');
const ngOut = join(dist, 'ui', 'browser');

function log(step) {
  console.log(`\n[build:ext] ${step}`);
}

// Invoke the locally-installed Angular CLI JS entry directly with the current
// Node binary — avoids spawning a shell (and its escaping pitfalls) for `npx`.
function ngBuild(args) {
  const ngBin = join(root, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');
  execFileSync(process.execPath, [ngBin, ...args], {
    cwd: root,
    stdio: 'inherit',
  });
}

// 1. Clean previous dist (the Angular builder writes into dist/ui).
log('cleaning dist/');
rmSync(dist, { recursive: true, force: true });

// 2. Angular UI build.
log('ng build (Angular UI)');
ngBuild(['build', '--configuration', 'production']);

if (!existsSync(ngOut)) {
  throw new Error(`Expected Angular output at ${ngOut} but it does not exist.`);
}

// 3. esbuild the worker (ESM) and content script (IIFE).
log('esbuild background.ts → background.js (ESM)');
await esbuild({
  entryPoints: [join(root, 'src', 'worker', 'background.ts')],
  outfile: join(dist, 'background.js'),
  bundle: true,
  format: 'esm',
  target: 'es2022',
  platform: 'browser',
  minify: true,
  sourcemap: false,
});

log('esbuild content.ts → content.js (IIFE)');
await esbuild({
  entryPoints: [join(root, 'src', 'content', 'content.ts')],
  outfile: join(dist, 'content.js'),
  bundle: true,
  format: 'iife',
  target: 'es2022',
  platform: 'browser',
  minify: true,
  sourcemap: false,
});

// 4. Copy the Angular bundle (main.js, styles.css, favicon, any chunks) to dist root.
log('copying Angular bundle → dist/');
for (const entry of readdirSync(ngOut)) {
  // index.html is the builder's single template; we ship our own per-surface
  // HTMLs instead, so skip it.
  if (entry === 'index.html') {
    continue;
  }
  const from = join(ngOut, entry);
  const to = join(dist, entry);
  if (statSync(from).isDirectory()) {
    cpSync(from, to, { recursive: true });
  } else {
    cpSync(from, to);
  }
}

// 5. Copy the three entry HTMLs.
log('copying entry HTMLs (popup/options/block)');
for (const page of ['popup.html', 'options.html', 'block.html']) {
  cpSync(join(root, 'src', 'ui', 'pages', page), join(dist, page));
}

// 6. Copy manifest + icons.
log('copying manifest.json + icons');
cpSync(join(root, 'src', 'manifest.json'), join(dist, 'manifest.json'));
mkdirSync(join(dist, 'icons'), { recursive: true });
cpSync(join(root, 'icons'), join(dist, 'icons'), { recursive: true });

// 7. Remove the intermediate dist/ui tree (its useful files are now at dist root).
rmSync(join(dist, 'ui'), { recursive: true, force: true });

log('done. Extension built into dist/');
console.log('  Load unpacked: chrome://extensions → Developer mode → Load unpacked → select the dist/ folder.');
