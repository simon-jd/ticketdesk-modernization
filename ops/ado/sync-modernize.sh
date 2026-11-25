#!/usr/bin/env bash
# Bring Azure Repos' modernize to a local commit (default: the local modernize) the only way the branch policy allows:
# push it to sync/modernize, open a PR into modernize, wait for the CI (build validation), complete it with "rebase and
# fast-forward" (no new commit: the remote ends at exactly the local SHA), delete sync/modernize.
#
#   ops/ado/sync-modernize.sh [REF] [--timeout SECONDS]      (run from any checkout of the shared repository)
#
# Refuses when Azure Repos' modernize is not an ancestor of REF (it has commits the local side lacks: merge those first,
# e.g. git fetch <repo url> modernize && git merge FETCH_HEAD). Exit 1 if the CI fails: the PR stays open for a fix.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"
REF="${1:-modernize}"; TIMEOUT=2400
[[ "${2:-}" == --timeout ]] && TIMEOUT="$3"
GIT=(git -C "$TD_W/ticketdesk")
sha="$("${GIT[@]}" rev-parse --verify "$REF^{commit}")"
remote="$(ado_api GET "_apis/git/repositories/$ADO_REPO/refs?filter=heads/modernize&api-version=7.1" | jq -r '.value[] | select(.name == "refs/heads/modernize") | .objectId')"
[[ "$remote" == "$sha" ]] && { echo "Azure Repos modernize is already at ${sha:0:8}"; exit 0; }
"${GIT[@]}" cat-file -e "$remote^{commit}" 2>/dev/null || ado_git -C "$TD_W/ticketdesk" fetch -q "$ADO_GIT_URL" refs/heads/modernize
"${GIT[@]}" merge-base --is-ancestor "$remote" "$sha" || { echo "Azure Repos modernize (${remote:0:8}) is not an ancestor of $REF (${sha:0:8}): merge it first" >&2; exit 1; }
echo "$(date -u +%FT%TZ) sync ${remote:0:8} -> ${sha:0:8} ($("${GIT[@]}" rev-list --count "$remote..$sha") commits)"
ado_git -C "$TD_W/ticketdesk" push -q -f "$ADO_GIT_URL" "$sha:refs/heads/sync/modernize"
pr="$("$HERE/pr.sh" create sync/modernize modernize "Sync modernize to ${sha:0:8}" "Local integration branch modernize at $sha (coordinator merge). Completed by ops/ado/sync-modernize.sh with rebase and fast-forward once the CI passes.")"
echo "PR $pr ($ADO_ORG_URL/$ADO_PROJECT/_git/$ADO_REPO/pullrequest/$pr)"
sleep 15
start=$(date +%s)
while :; do
  st="$(ado_api GET "_apis/policy/evaluations?artifactId=vstfs:///CodeReview/CodeReviewId/<ado-project-id>/$pr&api-version=7.1-preview.1" | jq -r '[.value[] | select(.configuration.type.displayName == "Build")][0].status // "none"')"
  case "$st" in approved) break ;; rejected) echo "CI failed: PR $pr stays open"; "$HERE/pr.sh" show "$pr"; exit 1 ;; esac
  (( $(date +%s) - start < TIMEOUT )) || { echo "timeout waiting for the CI (status $st)"; exit 124; }
  sleep 20
done
"$HERE/pr.sh" complete "$pr"
for _ in $(seq 1 15); do
  now="$(ado_api GET "_apis/git/repositories/$ADO_REPO/refs?filter=heads/modernize&api-version=7.1" | jq -r '.value[] | select(.name == "refs/heads/modernize") | .objectId')"
  [[ "$now" == "$sha" ]] && break; sleep 4
done
[[ "$now" == "$sha" ]] || { echo "modernize is at ${now:0:8}, expected ${sha:0:8}" >&2; exit 1; }
ado_git -C "$TD_W/ticketdesk" push -q "$ADO_GIT_URL" --delete refs/heads/sync/modernize
echo "$(date -u +%FT%TZ) Azure Repos modernize = ${sha:0:8} (PR $pr completed, fast-forward)"
