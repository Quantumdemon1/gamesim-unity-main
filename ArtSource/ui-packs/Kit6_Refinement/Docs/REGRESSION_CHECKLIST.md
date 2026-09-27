# Regression and integration checklist

No Unity tests were executed by this package-generation session. This file is a proposed acceptance list, not a test-result certificate.

## Shared visual and input checks
- Existing button captions, callbacks and control identities are retained in the skin-only patch.
- Intended layout changes have an explicitly approved rect-snapshot diff; do not mass-rebase the suite.
- Persistent resting rows do not use focus glow.
- Text remains readable with the world visible behind it and with grayscale inspection.
- Surface opacity does not multiply down the text opacity through a parent CanvasGroup.
- Decorative sprites never steal pointer events; genuine modal blockers block the world intentionally.
- Hidden views do not intercept input. Closing restores the previously focused control.
- Keyboard/controller navigation, long names, long localized strings and text scale are exercised.
- Test at 1920×1080, 1280×720, 2560×1440 and an approved ultrawide aspect ratio.
- Existing normal full right rail still exists after closing a notebook page; compact default does not change.
- Both the normal saved-preference path and the test override path are covered.
- The pinned baked night lighting test remains unchanged.

## Conversation unavailable
- No action spent and no relationship mutation on unavailable response.
- Short message panel has no large unused vertical tail.
- Mood and signed trust appear as separate fields.
- Existing Close semantics preserved; opening does not invent a countdown or unlock state.

## Location directory
- Active roster and knowledge-limited counts reconcile.
- A located character is not simultaneously listed in two rooms.
- Unknown location is not asserted empty, known, or timed without backing data.
- Empty/occupied filter is view-only and respects controller data.

## Houseguests/profile
- Houseguests navigation highlighted when that page is current.
- Status, mood, trust, and awards are bound to different fields.
- Juror records are not accidentally treated as active in-house occupants.
- No invented score range, inferred hostility band, biography, win count or secret.

## Vote history
- No-event, unavailable-record and load-failure states are distinct.
- Unknown ballots stay unknown, even if hidden global game state has the answer.
- A known result can exist without an individual ballot ledger.
- No fabricated tally or explicit “nobody voted” claim derived just from an empty query.

## Diary/confirmation
- Opening, tabbing, reading, and closing never apply a reflection, preparation gain, trust change or jury change.
- Existing score/ledger qualifications remain accessible.
- No-pending view shows no invented decision controls.
- Correct pending answer and exact effects appear in review.
- Confirm is idempotent against double input; Back discards.
- Existing ceremony progression remains at its authorized controller path.
- “Walk to the room” hint is absent while the player is already in the private view.
