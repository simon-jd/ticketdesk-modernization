#!/usr/bin/env bash
# CI: container images of the web app and the worker (linux/amd64, the same Dockerfiles as AKS), tagged ci-<run id> in the
# host's Docker (no registry push: the CD pipeline loads them into kind; the aks session pushes to its ACR). Smoke: the web
# image answers /health/live. Retention: azure-pipelines/scripts/image-prune.sh (nothing a CD run may still deploy is pruned).
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
cd "$CI_REPO"
TAG="ci-${BUILD_BUILDID:-local}"
REV="$(git rev-parse HEAD)"
OUT="$CI_OUT/images"; mkdir -p "$OUT"
for app in web worker; do
  ci_section "docker build ticketdesk-$app:$TAG"
  s=$(date +%s)
  docker build -q -f "src/modern/TicketDesk.${app^}/Dockerfile" --label "org.opencontainers.image.revision=$REV" \
    --label "ticketdesk.ci.build=${BUILD_BUILDNUMBER:-local}" -t "ticketdesk-$app:$TAG" . > /dev/null
  echo "ticketdesk-$app:$TAG $(docker image inspect -f '{{.Size}}' "ticketdesk-$app:$TAG") bytes, built in $(( $(date +%s) - s )) s" | tee -a "$OUT/images.txt"
done
ci_section "smoke: web image /health/live"
name="td-ci-smoke-${BUILD_BUILDID:-local}"
docker run -d --rm --name "$name" -p 127.0.0.1::8080 "ticketdesk-web:$TAG" > /dev/null
trap 'docker rm -f "$name" >/dev/null 2>&1 || true' EXIT
port="$(docker port "$name" 8080/tcp | head -1 | cut -d: -f2)"
for _ in $(seq 1 60); do curl -sf "http://127.0.0.1:$port/health/live" >/dev/null 2>&1 && break; sleep 1; done
curl -sf "http://127.0.0.1:$port/health/live" | head -c 200; echo
echo "uid: $(docker image inspect -f '{{.Config.User}}' "ticketdesk-web:$TAG")" | tee -a "$OUT/images.txt"
docker rm -f "$name" >/dev/null 2>&1 || true
# retention (image-prune.sh): this run's tag, the 3 newest, and every tag built less than 30 h ago (a CD run can deploy a tag
# until its kind-local approval times out, 24 h) stay; only older ci-* tags of these two repositories go
[[ "$TAG" =~ ^ci-[0-9]+$ ]] && bash "$CI_REPO/azure-pipelines/scripts/image-prune.sh" record "$TAG"
CI_IMAGE_KEEP_TAG="$TAG" bash "$CI_REPO/azure-pipelines/scripts/image-prune.sh" prune ticketdesk-web ticketdesk-worker | tee -a "$OUT/images.txt"
echo "##vso[task.setvariable variable=imageTag;isOutput=true]$TAG" >&3
