// Life Planner - Service Worker Registration
// Registers the service worker only in production (HTTPS) or localhost.

(function () {
  if (!('serviceWorker' in navigator)) return;

  window.addEventListener('load', function () {
    navigator.serviceWorker
      .register('/service-worker.js', { scope: '/' })
      .then(function (registration) {
        console.log('[PWA] Service Worker registered. Scope:', registration.scope);

        // Checks for updates every 60 minutes.
        setInterval(function () {
          registration.update();
        }, 60 * 60 * 1000);

        // Reloads when a new service worker version is ready.
        registration.addEventListener('updatefound', function () {
          const newWorker = registration.installing;
          if (!newWorker) return;

          newWorker.addEventListener('statechange', function () {
            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
              console.log('[PWA] New version available. The page will be refreshed.');
              // Reloads silently to apply the new version.
              window.location.reload();
            }
          });
        });
      })
      .catch(function (error) {
        console.warn('[PWA] Failed to register Service Worker:', error);
      });
  });
})();
