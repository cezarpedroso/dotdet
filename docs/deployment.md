# DotDet production deployment

DotDet should be deployed behind an HTTPS reverse proxy. Keep the frontend and API on the same origin when practical; for split-origin deployments, configure both sides explicitly.

## GitHub OAuth

Set these environment variables in production:

```text
Authentication__GitHub__ClientId=...
Authentication__GitHub__ClientSecret=...
Authentication__GitHub__FrontendBaseUrl=https://dotdet.example.com
Authentication__GitHub__CallbackPath=/signin-github
```

The GitHub OAuth application callback must exactly match the public API callback URL, for example `https://api.dotdet.example.com/signin-github`. Set the OAuth homepage URL to the public frontend origin. Production cookies require HTTPS.

## Proxy and CORS

Configure the reverse proxy to send `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`. Add only the proxy IP addresses DotDet should trust:

```text
Hosting__KnownProxies__0=10.0.0.10
Hosting__AllowedOrigins__0=https://dotdet.example.com
```

For a split-origin frontend, set `VITE_DOTDET_API_URL` at frontend build time. When it is omitted in production, the frontend uses its own origin. Development localhost origins remain in `appsettings.Development.json` only.

## Analysis limits

The defaults allow six analysis starts per caller per minute, one concurrent analysis per caller, and a five-minute execution timeout. Override with `AnalysisExecution__PermitLimit`, `AnalysisExecution__WindowSeconds`, `AnalysisExecution__MaxConcurrentPerCaller`, and `AnalysisExecution__TimeoutSeconds`.

Set `AllowedHosts` to the deployed API host. Uploaded and GitHub repositories continue to use safe syntax analysis unless an isolated semantic-analysis worker is explicitly configured.
