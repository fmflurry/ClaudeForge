# API Gateway Hook

Hook plugin for API gateway request/response transformation and security enforcement.

## Overview

The API Gateway Hook intercepts API gateway traffic to perform request validation,
response transformation, rate limiting enforcement, and security header injection
for DevOps and security teams.

## Installation

```bash
claude plugin install api-gateway-hook
```

## Configuration

No additional configuration required.

## Usage

Register the hook with your API gateway configuration. It will intercept all
requests and responses passing through the gateway.

## API

- `transformRequest(req: Request) -> Request` — transform an incoming request
- `enforceSecurityHeaders(resp: Response) -> Response` — inject security headers

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
