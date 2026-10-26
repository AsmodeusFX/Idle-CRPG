# Read-only audit evidence

Command: powershell -File tools/Audit-P2V16ReadOnly.ps1
Output: artifacts/qa/p2-4-v16/s15
Input files: 555; before/after SHA256 identical; source_hashes.csv lists each input.
Scope: production CSV, Core/UI source, all docs/temp inputs. Only artifacts/qa reports are written; no editor export, player save or production configuration write. This statement covers this script only, not earlier authorized R-A edits.
Discovery: rg --files docs/temp docs/suggest found S15 references but no original S06-S13 row-level candidate tables.
Status: PARTIAL_INPUT. Six requested artifacts are generated; full 31-star/98-talent/design-column mapping remains unavailable without original sources. Missing IDs and effects are not invented or marked passed.
