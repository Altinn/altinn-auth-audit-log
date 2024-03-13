#!/usr/bin/env bash

# Wire up the env and cli validations
__dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
# source "${__dir}/environment.sh"

REPO_ROOT=$(git rev-parse --show-toplevel)

cd "$REPO_ROOT" || exit 1
deno run -A ".github/$1.ts"
