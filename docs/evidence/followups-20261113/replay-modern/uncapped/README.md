# The 25-diffs cap (Round 5b)

Until Round 5b td-replay's comparer stopped listing an exchange's diffs at 25 (HTML and JSON walks), and report.json held
only those, so replay-summary.py and the CI gate never saw a diff past the 25th. Now the comparer keeps every diff in
report.json; `--max-diffs` only shortens report.md. The replay one level up (`../summary.md`, `b19f9f9`) is therefore
uncapped: 2,079 diffs counted, 22 exchanges with more than 25 (at most 86), **0 unexplained**.

Uncapped, `search#003.0` and `search#004.0` had diffs in result panels at positions 22-30 of `fieldset[1]/div[1]`, outside F1's
range 2-21: td-replay numbers aligned positions, not panels, so 20 legacy and 20 new partly different panels take positions
2-41. F1's range is 2-41 since Round 5b (expected-differences.md, F1).

The Round 4 candidate (`docs/evidence/retire-20261110/replay-modern/modern.jsonl.xz`, the "0 unexplained" of Round 4, measured
under the cap), compared without the cap:
- with the Round 4 rules (`be095b9`): **2 unexplained** (`search#003.0`, `#004.0`) - `summary-round4-candidate-round4-rules.md`
- with the Round 5b rules (F1 widened): 0 unexplained - `summary-round4-candidate-round5b-rules.md`
