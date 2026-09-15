#!/usr/bin/env bash
# Exercise recovery without connecting to Kubernetes.
set -euo pipefail
cd "$(dirname "$0")/../.."
test_dir=$(mktemp -d)
trap 'rm -rf "$test_dir"' EXIT
export NAMESPACE=bush-point IMAGE=example/image@sha256:test

jq() {
  if [ "$1" = -e ]; then return 0; fi
  printf '{}\n'
}
kubectl() {
  echo "$*" >> "$RUNNER_TEMP/commands"
  if [ "$1 $2" = 'apply -n' ] && [[ "$*" == *inference-deployment.yaml ]]; then
    case "$SCENARIO" in
      failure|recovery_failure) return 1 ;;
      interrupt) kill -INT "$$" ;;
      terminate) kill -TERM "$$" ;;
    esac
  fi
  if [ "$1" = replace ]; then
    cat > /dev/null
    if [ "$SCENARIO" = recovery_failure ]; then return 1; fi
  fi
  printf '{}\n'
}
export -f jq kubectl

for SCENARIO in success failure interrupt terminate recovery_failure; do
  export SCENARIO RUNNER_TEMP="$test_dir/$SCENARIO"
  mkdir -p "$RUNNER_TEMP"
  status=0
  bash .github/scripts/deploy-inference.sh > "$RUNNER_TEMP/output" 2>&1 || status=$?
  case "$SCENARIO" in
    success)
      test "$status" = 0
      ! grep -q '^replace ' "$RUNNER_TEMP/commands"
      ;;
    recovery_failure)
      test "$status" = 1
      grep -q 'Recovery failed' "$RUNNER_TEMP/output"
      test "$(grep -c '^replace ' "$RUNNER_TEMP/commands")" = 1
      ;;
    *)
      case "$SCENARIO" in
        failure) test "$status" = 1 ;;
        interrupt) test "$status" = 130 ;;
        terminate) test "$status" = 143 ;;
      esac
      test "$(grep -c '^replace ' "$RUNNER_TEMP/commands")" = 2
      grep -q 'scale deployment/inference-system --replicas=1' "$RUNNER_TEMP/commands"
      grep -q 'Previous ConfigMap and deployment restored' "$RUNNER_TEMP/output"
      ;;
  esac
  echo "PASS: $SCENARIO"
done
