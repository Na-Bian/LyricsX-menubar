#!/usr/bin/env bash

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "${HERE}/../../.." && pwd)"
WORKFLOW="${ROOT}/.github/workflows/community-release.yml"

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

[ -f "$WORKFLOW" ] || fail "Missing ${WORKFLOW}"

ruby - "$WORKFLOW" <<'RUBY'
require "yaml"

path = ARGV.fetch(0)
workflow = YAML.safe_load(File.read(path), [], [], true, aliases: true)
abort "FAIL: workflow root must be a mapping" unless workflow.is_a?(Hash)

on = workflow["on"] || workflow[true]
dispatch = on.is_a?(Hash) && on["workflow_dispatch"]
abort "FAIL: workflow_dispatch is missing" unless dispatch.is_a?(Hash)

version = dispatch.dig("inputs", "version")
abort "FAIL: required string input version is missing" unless version.is_a?(Hash) && version["required"] == true && version["type"] == "string"

abort "FAIL: contents: write permission is missing" unless workflow.dig("permissions", "contents") == "write"

job = workflow.dig("jobs", "release")
abort "FAIL: release job is missing" unless job.is_a?(Hash)
abort "FAIL: release job must use macos-26" unless job["runs-on"] == "macos-26"
expected_developer_dir = "/Applications/Xcode_26.4.app/Contents/Developer"
abort "FAIL: DEVELOPER_DIR is incorrect" unless job.dig("env", "DEVELOPER_DIR") == expected_developer_dir

steps = job["steps"]
abort "FAIL: release steps are missing" unless steps.is_a?(Array)

checkout = steps.find { |step| step["uses"] == "actions/checkout@v4" }
abort "FAIL: checkout with fetch-depth 0 is missing" unless checkout && checkout.dig("with", "fetch-depth") == 0

expected_scripts = [
  "community-resolve-version.sh",
  "community-build.sh",
  "package.sh",
  "community-compose-notes.sh",
  "community-create-release.sh"
]
actual_scripts = steps.map do |step|
  run = step["run"]
  next unless run.is_a?(String)
  expected_scripts.find { |script| run.include?("Scripts/release/#{script}") }
end.compact
abort "FAIL: release scripts are missing or out of order: #{actual_scripts.inspect}" unless actual_scripts == expected_scripts

diagnostics = steps.find { |step| step["uses"] == "actions/upload-artifact@v4" }
unless diagnostics && diagnostics["if"] == "failure()" && diagnostics.dig("with", "path") == "build/" && diagnostics.dig("with", "retention-days") == 7
  abort "FAIL: seven-day build diagnostics upload on failure is missing"
end
RUBY

SECRET_NAMES="$(sed -nE 's/.*secrets\.([A-Za-z0-9_]+).*/\1/p' "$WORKFLOW" | sort -u)"
[ "$SECRET_NAMES" = "GITHUB_TOKEN" ] \
    || fail "Only secrets.GITHUB_TOKEN may be referenced; found: ${SECRET_NAMES:-none}"

for prohibited in \
    'APPLE_' \
    'KEYCHAIN_' \
    'SPARKLE_' \
    'PAGES_MIRROR_' \
    'notarize.sh' \
    'sign-sparkle.sh' \
    'publish-appcast.sh'; do
    if grep -Fq "$prohibited" "$WORKFLOW"; then
        fail "Community workflow contains prohibited dependency: ${prohibited}"
    fi
done

echo 'PASS: community workflow is isolated and publishes with only GITHUB_TOKEN'
