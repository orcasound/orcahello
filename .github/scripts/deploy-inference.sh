#!/usr/bin/env bash
# Both release and ConfigMap workflows hold the namespace concurrency lock.
# Required inputs: NAMESPACE and RUNNER_TEMP. Set IMAGE for an image release;
# leave it unset for a ConfigMap-only update that preserves the live deployment.
set -euo pipefail

case "$NAMESPACE" in
  andrews-bay|bush-point|mast-center|north-sjc|orcasound-lab|point-robinson|port-townsend|sunset-bay) ;;
  *) echo "Unsupported hydrophone namespace: $NAMESPACE" >&2; exit 1 ;;
esac

backup="$RUNNER_TEMP/inference-backup"
mkdir -p "$backup"
kubectl get deployment/inference-system -n "$NAMESPACE" -o json > "$backup/deployment.json"
kubectl get configmap/hydrophone-configs -n "$NAMESPACE" -o json > "$backup/configmap.json"
jq -e '.spec.replicas == 1' "$backup/deployment.json" > /dev/null || {
  echo "Expected one replica in $NAMESPACE before deployment" >&2
  exit 1
}

manifest="$RUNNER_TEMP/inference-deployment.yaml"
if [ -n "${IMAGE:-}" ]; then
  # Image releases apply the complete repository manifest with the selected image.
  kubectl set image --local -f "InferenceSystem/deploy/$NAMESPACE.yaml" inference-system="$IMAGE" -o yaml > "$manifest"
else
  # ConfigMap-only updates preserve the live image and deployment settings.
  jq 'del(.status, .metadata.managedFields, .metadata.resourceVersion)' "$backup/deployment.json" > "$manifest"
fi

stop_pods() {
  kubectl scale deployment/inference-system --replicas=0 -n "$NAMESPACE" &&
  kubectl wait --for=delete pod -l app=inference-system -n "$NAMESPACE" --timeout=5m
}

start_pods() {
  kubectl scale deployment/inference-system --replicas=1 -n "$NAMESPACE" &&
  kubectl rollout status deployment/inference-system -n "$NAMESPACE" --timeout=10m
}

deploy() {
  kubectl apply -n "$NAMESPACE" -f "InferenceSystem/deploy/$NAMESPACE-configmap.yaml" &&
  stop_pods &&
  kubectl apply -n "$NAMESPACE" -f "$manifest" &&
  start_pods
}

restore_resource() {
  # Replace the complete saved object, including fields removed by this deployment.
  local version
  version=$(kubectl get "$1" -n "$NAMESPACE" -o jsonpath='{.metadata.resourceVersion}') &&
  jq --arg version "$version" '
    .metadata.resourceVersion = $version |
    del(.status, .metadata.managedFields) |
    if .kind == "Deployment" then .spec.replicas = 0 else . end
  ' "$2" | kubectl replace -n "$NAMESPACE" -f -
}

recover() {
  local status=$?
  # Avoid recursive recovery if restoration fails or another signal arrives.
  trap - EXIT
  trap '' INT TERM
  echo "Deployment failed or interrupted; restoring the saved ConfigMap and deployment in $NAMESPACE"
  if stop_pods &&
     restore_resource configmap/hydrophone-configs "$backup/configmap.json" &&
     restore_resource deployment/inference-system "$backup/deployment.json" &&
     start_pods; then
    echo "Previous ConfigMap and deployment restored"
  else
    echo "Recovery failed; manual intervention is required in $NAMESPACE" >&2
  fi
  exit "$status"
}

# Install recovery only after backups and local manifest preparation succeed.
# Cancellation recovery is best effort: a forced kill cannot run shell traps.
trap recover EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
deploy || exit 1
trap - EXIT INT TERM

kubectl get deployment/inference-system -n "$NAMESPACE" -o wide
