import { describe, expect, it } from 'vitest';

import { hostOf, youTubeThumbnail } from './url-utils';

describe('hostOf', () => {
  it('returns the hostname', () => {
    expect(hostOf('https://reddit.com/r/all')).toBe('reddit.com');
  });

  it('returns empty string for invalid input', () => {
    expect(hostOf('not a url')).toBe('');
  });
});

describe('youTubeThumbnail', () => {
  it('builds a thumbnail for a watch URL', () => {
    expect(youTubeThumbnail('https://www.youtube.com/watch?v=abc123')).toBe(
      'https://img.youtube.com/vi/abc123/hqdefault.jpg',
    );
  });

  it('builds a thumbnail for a youtu.be short link', () => {
    expect(youTubeThumbnail('https://youtu.be/xyz789')).toBe(
      'https://img.youtube.com/vi/xyz789/hqdefault.jpg',
    );
  });

  it('returns null for non-YouTube URLs', () => {
    expect(youTubeThumbnail('https://example.com/page')).toBeNull();
  });

  it('returns null for a YouTube URL without a video id', () => {
    expect(youTubeThumbnail('https://www.youtube.com/feed/subscriptions')).toBeNull();
  });
});
