/**
 * Tests for src/commands/publish.ts
 *
 * Production module path: src/commands/publish.ts
 * Exported functions:
 *   - runPublish(args: PublishArgs, deps: PublishDeps): Promise<CommandResult>
 *       args: { pluginPath?: string }   — defaults to cwd
 *       deps: {
 *         client: IMarketplaceClient;
 *         homeDir: string;
 *         fs?: PublishFsPort;
 *         env?: NodeJS.ProcessEnv;
 *       }
 *   - PublishFsPort: {
 *       readFile(p: string): Promise<string>;
 *       exists(p: string): Promise<boolean>;
 *       compress(dir: string): Promise<Blob>;   — tar.gz the directory
 *     }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * VERBATIM spec strings:
 *   - success: "Published @namespace/plugin-name@1.0.0 at https://marketplace.local/plugins/@namespace/plugin-name"
 *   - missing field: "Missing required field: <field>"
 *   - duplicate version: "Version 1.0.0 of @namespace/plugin-name already exists"
 *   - duplicate hint: "Suggest incrementing the version in plugin.json"
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/commands/publish.ts is created (RED state).
import { runPublish } from '../commands/publish.js';
import type { CommandResult, PublishFsPort } from '../commands/publish.js';
import type { IMarketplaceClient, UploadResponse } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';
import type { ProblemDetails } from '../api/client.js';
import { SessionExpiredError } from '../auth/token-attachment.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeValidManifestJson(overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    name: '@namespace/plugin-name',
    version: '1.0.0',
    description: 'A test plugin',
    author: 'Test Author',
    types: ['skill'],
    languages: ['typescript'],
    entrypoints: ['src/index.ts'],
    ...overrides,
  });
}

function makeFakeFs(manifestJson: string, overrides?: Partial<PublishFsPort>): PublishFsPort {
  return {
    readFile: vi.fn(async (p: string) => {
      if (p.endsWith('plugin.json')) return manifestJson;
      throw Object.assign(new Error('ENOENT'), { code: 'ENOENT' });
    }),
    exists: vi.fn(async (p: string) => p.endsWith('plugin.json')),
    compress: vi.fn(async () => new Blob(['fake-archive-content'])),
    ...overrides,
  };
}

const SUCCESS_UPLOAD: UploadResponse = {
  id: 'abc123',
  name: '@namespace/plugin-name',
  slug: 'namespace-plugin-name',
  version: '1.0.0',
};

function makeFakeClient(overrides?: Partial<IMarketplaceClient>): IMarketplaceClient {
  return {
    searchPlugins: vi.fn(),
    getPlugin: vi.fn(),
    downloadPlugin: vi.fn(),
    uploadPlugin: vi.fn().mockResolvedValue(SUCCESS_UPLOAD),
    getLatestVersion: vi.fn(),
    checkVersionExists: vi.fn().mockResolvedValue(false),
    ...overrides,
  };
}

function make409Error(): MarketplaceApiError {
  const pd: ProblemDetails = {
    title: 'Conflict',
    status: 409,
    detail: 'Version 1.0.0 of @namespace/plugin-name already exists',
  };
  return new MarketplaceApiError(pd, 409);
}

// ---------------------------------------------------------------------------
// runPublish – happy path
// ---------------------------------------------------------------------------

describe('runPublish – happy path', () => {
  it('returns exitCode 0 on successful publish', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result: CommandResult = await runPublish(
      { pluginPath: '/my/plugin' },
      { client, homeDir: '/tmp/home', fs: fakeFs },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output contains "Published @namespace/plugin-name@1.0.0"', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.output).toContain('Published @namespace/plugin-name@1.0.0');
  });

  it('output contains a marketplace URL', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    // Spec: "Published @namespace/plugin-name@1.0.0 at https://..."
    expect(result.output).toMatch(/at https?:\/\//);
  });

  it('calls client.uploadPlugin with a FormData containing the package', async () => {
    const uploadFn = vi.fn().mockResolvedValue(SUCCESS_UPLOAD);
    const client = makeFakeClient({ uploadPlugin: uploadFn });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(uploadFn).toHaveBeenCalled();
    const [formData] = uploadFn.mock.calls[0] as [FormData];
    expect(formData).toBeInstanceOf(FormData);
  });

  it('calls compress to archive the plugin directory before upload', async () => {
    const compressFn = vi.fn(async () => new Blob(['archive']));
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(makeValidManifestJson(), { compress: compressFn });
    await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(compressFn).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runPublish – missing manifest fields
// ---------------------------------------------------------------------------

describe('runPublish – missing required metadata', () => {
  it('returns non-zero exitCode when plugin.json is missing required fields', async () => {
    const incompleteManifest = JSON.stringify({ name: '@namespace/plugin-name', version: '1.0.0' });
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(incompleteManifest);
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('output reports which fields are missing', async () => {
    const incompleteManifest = JSON.stringify({ name: '@namespace/plugin-name', version: '1.0.0' });
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(incompleteManifest);
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    // Spec: "Missing required field: type"
    expect(result.output).toContain('Missing required field');
  });

  it('output suggests using claude plugin scaffold', async () => {
    const incompleteManifest = JSON.stringify({ name: '@namespace/plugin-name' });
    const client = makeFakeClient();
    const fakeFs = makeFakeFs(incompleteManifest);
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.output).toContain('claude plugin scaffold');
  });

  it('does NOT call client.uploadPlugin when manifest validation fails', async () => {
    const uploadFn = vi.fn();
    const client = makeFakeClient({ uploadPlugin: uploadFn });
    const fakeFs = makeFakeFs(JSON.stringify({ name: '@test/plugin' }));
    await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(uploadFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runPublish – duplicate version
// ---------------------------------------------------------------------------

describe('runPublish – duplicate version', () => {
  it('returns non-zero exitCode on 409 from API', async () => {
    const client = makeFakeClient({
      uploadPlugin: vi.fn().mockRejectedValue(make409Error()),
    });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('output says "Version 1.0.0 of @namespace/plugin-name already exists"', async () => {
    const client = makeFakeClient({
      uploadPlugin: vi.fn().mockRejectedValue(make409Error()),
    });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.output).toContain('Version 1.0.0 of @namespace/plugin-name already exists');
  });

  it('suggests incrementing the version in plugin.json', async () => {
    const client = makeFakeClient({
      uploadPlugin: vi.fn().mockRejectedValue(make409Error()),
    });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.output.toLowerCase()).toContain('incrementing');
  });
});

// ---------------------------------------------------------------------------
// runPublish – missing plugin.json
// ---------------------------------------------------------------------------

describe('runPublish – missing plugin.json', () => {
  it('returns non-zero exitCode when plugin.json is absent', async () => {
    const client = makeFakeClient();
    const fakeFs: PublishFsPort = {
      readFile: vi.fn(async () => {
        throw Object.assign(new Error('ENOENT'), { code: 'ENOENT' });
      }),
      exists: vi.fn(async () => false),
      compress: vi.fn(async () => new Blob()),
    };
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('output mentions plugin.json when manifest is missing', async () => {
    const client = makeFakeClient();
    const fakeFs: PublishFsPort = {
      readFile: vi.fn(async () => {
        throw Object.assign(new Error('ENOENT'), { code: 'ENOENT' });
      }),
      exists: vi.fn(async () => false),
      compress: vi.fn(async () => new Blob()),
    };
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.output).toContain('plugin.json');
  });
});

// ---------------------------------------------------------------------------
// runPublish – authentication
// ---------------------------------------------------------------------------

describe('runPublish – authentication', () => {
  it('returns non-zero exitCode and SessionExpiredError message when client throws SessionExpiredError', async () => {
    const client = makeFakeClient({
      uploadPlugin: vi.fn().mockRejectedValue(new SessionExpiredError()),
    });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    const result = await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(result.exitCode).toBe(1);
    expect(result.output).toContain('Session expired');
    expect(result.output).toContain('claude-plugin login');
  });

  it('appends org to FormData when --org is provided', async () => {
    const uploadFn = vi.fn().mockResolvedValue(SUCCESS_UPLOAD);
    const client = makeFakeClient({ uploadPlugin: uploadFn });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    await runPublish({ pluginPath: '/my/plugin', org: 'my-org' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    expect(uploadFn).toHaveBeenCalled();
    const [formData] = uploadFn.mock.calls[0] as [FormData];
    expect(formData.get('orgId')).toBe('my-org');
  });

  it('does NOT append orgId to FormData when --org is not provided', async () => {
    const uploadFn = vi.fn().mockResolvedValue(SUCCESS_UPLOAD);
    const client = makeFakeClient({ uploadPlugin: uploadFn });
    const fakeFs = makeFakeFs(makeValidManifestJson());
    await runPublish({ pluginPath: '/my/plugin' }, { client, homeDir: '/tmp/home', fs: fakeFs });
    const [formData] = uploadFn.mock.calls[0] as [FormData];
    expect(formData.get('orgId')).toBeNull();
  });
});
