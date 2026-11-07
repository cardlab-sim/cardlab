# Estimate: 002-full-balance-payment (CL-3 slice 1)

Checked by `python -I tools/estimate_check.py docs/features/002-full-balance-payment/estimate.md`
(fails on a committed XL or a two-size jump between gates without an override; warns when human
and agent differ by two or more sizes). The whole CL-3 ticket was XL and was split into four
slices; this file sizes slice 1 only.

Sizes: XS, S, M, L, XL. One row per gate; the human's blind size is given before the agent's.

| Gate | Human (blind) | Agent | Committed | Override (who: reason) |
|------|---------------|-------|-----------|------------------------|
| 1    | M             | L     | L         |                        |

Gate 1: committed L decided by the human in requirements round 1 (Q-6); see
[01-requirements.md §16](./01-requirements.md#16-t-shirt-size).
