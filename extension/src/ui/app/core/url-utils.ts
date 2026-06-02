/** Small pure URL helpers shared by surfaces (and unit-tested). */

export function hostOf(url: string): string {
  try {
    return new URL(url).hostname;
  } catch {
    return '';
  }
}

/** Returns a YouTube thumbnail URL for a watch/short link, or null otherwise. */
export function youTubeThumbnail(url: string): string | null {
  try {
    const u = new URL(url);
    let id = '';
    if (u.hostname.endsWith('youtube.com')) {
      id = u.searchParams.get('v') ?? '';
    } else if (u.hostname === 'youtu.be') {
      id = u.pathname.slice(1);
    }
    return id ? `https://img.youtube.com/vi/${id}/hqdefault.jpg` : null;
  } catch {
    return null;
  }
}
