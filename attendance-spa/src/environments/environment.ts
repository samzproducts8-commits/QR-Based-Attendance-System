// Same-origin API access: the Angular dev server proxies /api and /hubs to the
// backend (see proxy.conf.json). Using relative paths means the browser only
// ever talks to the one origin it loaded the SPA from — so:
//   • no hardcoded LAN IP to edit when the router reassigns it,
//   • no CORS, and
//   • no mixed-content when the SPA is served over HTTPS (required for the
//     phone camera), because the API rides the same HTTPS origin via the proxy.
export const environment = {
  production: false,
  apiUrl: '/api',
  hubUrl: '/hubs/attendance'
};
