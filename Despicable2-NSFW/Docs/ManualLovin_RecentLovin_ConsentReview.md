# Manual Lovin recent-lovin cooldown and consent review

## Change

Ordered manual pair lovin now ignores the recent-lovin cooldown even when Intimacy is installed.

Before this patch, the Intimacy validation bridge still passed `allowRecentLovin: false` for manual pair lovin, so the bridge could veto ordered lovin with the recent-lovin block even though Despicable's native manual path is intentionally lenient there.

## Current manual lovin gate review

### Without Intimacy installed

General hard blocks always apply:
- missing pawn / same pawn / non-humanlike / dead / underage
- health blockers from `GetHealthCheckFailureReason`
- ordered pawn aggressive mental state
- other pawn availability blockers

Recent lovin:
- ordered manual pair lovin ignores recent lovin
- manual self lovin ignores recent lovin
- autonomous lovin still respects it

Consent setting (`lovinMutualConsent`):
- ON: other pawn must also pass relations check
- OFF: other pawn relation opinion / compatibility is not required
- regardless of setting, other pawn orientation is still required

Ideology setting (`lovinRespectIdeology`):
- ON: other pawn ideology veto can block
- OFF: ideology does not block manual lovin

### With Intimacy installed

General hard blocks and availability checks come from the Intimacy bridge.

Recent lovin:
- ordered manual pair lovin now ignores recent lovin
- manual self lovin already ignored recent lovin

Consent / ideology behavior under the current bridge:
- the bridge still uses Intimacy's pair approval checks
- that means mutual attraction / mutual tolerance / ideology vetoes can still block even if Despicable's `lovinMutualConsent` or `lovinRespectIdeology` settings are off

So the recent-lovin mismatch is fixed by this patch, but the Intimacy bridge still behaves as an approval oracle rather than mirroring Despicable's consent toggles exactly.
