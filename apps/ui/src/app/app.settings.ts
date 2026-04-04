declare global {
  interface Window {
    __COG_SLOP_API_BASE_URL__?: string;
  }
}

function trimTrailingSlash(value: string): string {
  return value.replace(/\/+$/, '');
}

const productionApiBaseUrl = 'https://cog-slop.azurewebsites.net';

function resolveApiBaseUrl(): string {
  const runtimeOverride = window.__COG_SLOP_API_BASE_URL__?.trim();
  if (runtimeOverride) {
    return trimTrailingSlash(runtimeOverride);
  }

  const host = window.location.hostname.toLowerCase();
  if (host === 'localhost' || host === '127.0.0.1') {
    return 'https://localhost:7298';
  }

  return productionApiBaseUrl;
}

export const appSettings = {
  apiBaseUrl: resolveApiBaseUrl()
};
