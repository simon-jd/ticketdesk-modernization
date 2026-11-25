#!/usr/bin/env bash
# Pull requests of the TicketDesk repo in Azure Repos (PAT via ops/ado/ado-lib.sh). Used for the gate demos and the
# modernize sync; completing a PR is not an approval (the branch policy on modernize is build validation only).
#
#   ops/ado/pr.sh create SOURCE TARGET TITLE [DESCRIPTION]   prints the PR id
#   ops/ado/pr.sh show PR              status, merge status, source/target commits, policy evaluations (build result)
#   ops/ado/pr.sh wait-policy PR [S]   wait until the build policy is no longer queued/running (default 1800 s)
#   ops/ado/pr.sh complete PR          complete with "rebase and fast-forward" (the target ends at the source's commits)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"
PROJECT_ID=<ado-project-id>
pr_json() { ado_api GET "_apis/git/repositories/$ADO_REPO/pullrequests/$1?api-version=7.1"; }
evals() { ado_api GET "_apis/policy/evaluations?artifactId=vstfs:///CodeReview/CodeReviewId/$PROJECT_ID/$1&api-version=7.1-preview.1"; }

case "${1:-}" in
  create)
    jq -n --arg s "refs/heads/$2" --arg t "refs/heads/$3" --arg ti "$4" --arg d "${5:-}" \
      '{sourceRefName: $s, targetRefName: $t, title: $ti, description: $d}' \
      | ado_api POST "_apis/git/repositories/$ADO_REPO/pullrequests?api-version=7.1" - | jq -r '.pullRequestId' ;;
  show)
    pr_json "$2" | jq -r '"PR \(.pullRequestId) \(.status) merge=\(.mergeStatus) \(.sourceRefName|sub("refs/heads/";"")) -> \(.targetRefName|sub("refs/heads/";"")) source=\(.lastMergeSourceCommit.commitId[0:8]) merged=\(.lastMergeCommit.commitId[0:8] // "-") closed=\(.closedDate // "-") \"\(.title)\""'
    evals "$2" | jq -r '.value[] | "  policy \(.configuration.type.displayName) blocking=\(.configuration.isBlocking) status=\(.status) build=\(.context.buildId // "-") buildOutputPreview=\(.context.buildOutputPreview.jobName // "")"' ;;
  wait-policy)
    t="${3:-1800}"; s=$(date +%s)
    while :; do
      st="$(evals "$2" | jq -r '[.value[] | select(.configuration.type.displayName == "Build") | .status][0] // "none"')"
      [[ "$st" == queued || "$st" == running || "$st" == none ]] || { echo "build policy: $st"; break; }
      (( $(date +%s) - s < t )) || { echo "timeout ($st)"; exit 124; }
      sleep 20
    done ;;
  complete)
    src="$(pr_json "$2" | jq -r '.lastMergeSourceCommit.commitId')"
    jq -n --arg c "$src" '{status: "completed", lastMergeSourceCommit: {commitId: $c},
      completionOptions: {mergeStrategy: "rebase", deleteSourceBranch: false, transitionWorkItems: false}}' \
      | ado_api PATCH "_apis/git/repositories/$ADO_REPO/pullrequests/$2?api-version=7.1" - | jq -r '"PR \(.pullRequestId) -> \(.status) merge=\(.mergeStatus)"' ;;
  *) sed -n '2,9p' "$0"; exit 2 ;;
esac
