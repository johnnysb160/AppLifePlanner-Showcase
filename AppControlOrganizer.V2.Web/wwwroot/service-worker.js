// Life Planner - Service Worker
// Blazor Server PWA: caches only static assets.
// Dynamic content (SignalR / API) always goes directly to the server.

const CACHE_VERSION = 'life-planner-v1';

// Static assets pre-cached during installation.
const PRECACHE_ASSETS = [
  '/css/app.css',
  '/icons/icon-192x192.png',
  '/icons/icon-512x512.png',
  '/icons/icon-180x180.png',
  '/Files/favicon.png',
  '/Files/Logo.png',
];

// Install: pre-caches essential assets.
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_VERSION).then(cache => {
      return cache.addAll(PRECACHE_ASSETS);
    }).then(() => self.skipWaiting())
  );
});

// Activate: removes old cache versions.
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys
          .filter(key => key !== CACHE_VERSION)
          .map(key => caches.delete(key))
      )
    ).then(() => self.clients.claim())
  );
});

// Fetch: strategy by request type.
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // Ignores non-GET requests.
  if (event.request.method !== 'GET') return;

  // Never intercepts Blazor SignalR, APIs, or authentication.
  if (
    url.pathname.startsWith('/_blazor') ||
    url.pathname.startsWith('/_framework') ||
    url.pathname.startsWith('/api/') ||
    url.pathname.startsWith('/auth/') ||
    url.pathname.includes('signalr') ||
    url.pathname.includes('negotiate')
  ) {
    return;
  }

  // Navigation (HTML): network-first with root cache fallback.
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request)
        .then(response => {
          // Caches the home page for offline fallback.
          if (url.pathname === '/') {
            const clone = response.clone();
            caches.open(CACHE_VERSION).then(c => c.put(event.request, clone));
          }
          return response;
        })
        .catch(() => caches.match('/') || caches.match(event.request))
    );
    return;
  }

  // Static assets (CSS, JS, images, fonts): cache-first with network fallback.
  const isStaticAsset =
    url.pathname.startsWith('/css/') ||
    url.pathname.startsWith('/js/') ||
    url.pathname.startsWith('/icons/') ||
    url.pathname.startsWith('/Files/') ||
    url.pathname.endsWith('.css') ||
    url.pathname.endsWith('.js') ||
    url.pathname.endsWith('.png') ||
    url.pathname.endsWith('.jpg') ||
    url.pathname.endsWith('.svg') ||
    url.pathname.endsWith('.woff') ||
    url.pathname.endsWith('.woff2') ||
    // CDN assets (Bootstrap, Bootstrap Icons, Chart.js)
    url.hostname.includes('cdn.jsdelivr.net') ||
    url.hostname.includes('cdnjs.cloudflare.com');

  if (isStaticAsset) {
    event.respondWith(
      caches.match(event.request).then(cached => {
        if (cached) return cached;

        return fetch(event.request).then(response => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE_VERSION).then(c => c.put(event.request, clone));
          }
          return response;
        });
      })
    );
  }
});
