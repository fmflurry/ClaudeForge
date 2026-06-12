import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { request as httpRequest } from 'node:http';
import { join } from 'node:path';
import { URL } from 'node:url';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Dev-only proxy: forwards /api, /auth, and /health to the backend.
 *
 * Active when NODE_ENV !== 'production'. Target precedence:
 *   1. DEV_API_PROXY_TARGET  – explicit override always wins
 *   2. SSR_API_BASE_URL      – set by docker-compose for the web container
 *                              (e.g. http://api:8080), used when running inside Docker
 *   3. http://localhost:5010 – fallback for local `dev:ssr` outside Docker,
 *                              matching the host port mapped by docker-compose
 */
if (process.env['NODE_ENV'] !== 'production') {
  const proxyTarget = process.env['DEV_API_PROXY_TARGET'] ?? process.env['SSR_API_BASE_URL'] ?? 'http://localhost:5010';
  const proxyPrefixes = ['/api', '/auth', '/health'];

  app.use((req: express.Request, res: express.Response, next: express.NextFunction): void => {
    const shouldProxy = proxyPrefixes.some((prefix) => req.url.startsWith(prefix));
    if (!shouldProxy) {
      next();
      return;
    }

    const targetUrl = new URL(req.url, proxyTarget);
    const options = {
      hostname: targetUrl.hostname,
      port: targetUrl.port || 80,
      path: targetUrl.pathname + targetUrl.search,
      method: req.method,
      headers: {
        ...req.headers,
        host: targetUrl.host,
      },
    };

    const proxyReq = httpRequest(options, (proxyRes) => {
      res.writeHead(proxyRes.statusCode ?? 502, proxyRes.headers as Record<string, string>);
      proxyRes.pipe(res, { end: true });
    });

    proxyReq.on('error', (err: Error) => {
      console.error(`[dev-proxy] Failed to proxy ${req.method} ${req.url} → ${proxyTarget}:`, err.message);
      if (!res.headersSent) {
        res.status(502).json({ error: 'Dev proxy upstream unavailable', detail: err.message });
      }
    });

    req.pipe(proxyReq, { end: true });
  });
}

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req: express.Request, res: express.Response, next: express.NextFunction) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] ?? 4000;
  app.listen(port, (error?: Error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
