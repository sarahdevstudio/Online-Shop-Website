/**
 * Sign-in attempts per key (IP + e-mail) in a sliding window, in memory (one Next.js instance). On top of this, the API
 * limits per IP and per account and locks an account for 15 minutes after 5 wrong passwords.
 */
export class LoginLimiter {
  private readonly attempts = new Map<string, number[]>();

  constructor(
    private readonly limit = 5,
    private readonly windowMs = 60_000,
    private readonly now: () => number = Date.now,
  ) {}

  /** Records an attempt; false when the key has used up its attempts in the window. */
  tryAcquire(key: string): boolean {
    const now = this.now();
    const recent = (this.attempts.get(key) ?? []).filter((t) => now - t < this.windowMs);
    if (recent.length >= this.limit) {
      this.attempts.set(key, recent);
      return false;
    }
    recent.push(now);
    this.attempts.set(key, recent);
    if (this.attempts.size > 5000) this.prune(now);
    return true;
  }

  private prune(now: number) {
    for (const [key, times] of this.attempts) {
      if (times.every((t) => now - t >= this.windowMs)) this.attempts.delete(key);
    }
  }
}

export const loginLimiter = new LoginLimiter();
