/**
 * Formats a numeric metric count into a human-readable abbreviated string.
 *
 * - >= 1,000,000 → "${n}M" (one decimal place)
 * - >= 1,000     → "${n}k" (one decimal place)
 * - otherwise    → plain number string
 */
export function formatMetricCount(count: number): string {
  if (count >= 1_000_000) {
    return `${(count / 1_000_000).toFixed(1)}M`;
  }
  if (count >= 1_000) {
    return `${(count / 1_000).toFixed(1)}k`;
  }
  return count.toString();
}
