// Tells the server the browser's IANA time zone so times render in local time (static SSR has no other way to know).
(function () {
  try {
    var zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!zone) return;
    var cookie = 'harmonee_tz=' + encodeURIComponent(zone);
    if (document.cookie.split('; ').indexOf(cookie) === -1) {
      document.cookie = cookie + '; path=/; max-age=31536000; samesite=lax; secure';
    }
  } catch (e) {
    // Older browsers: times stay in UTC.
  }
})();
