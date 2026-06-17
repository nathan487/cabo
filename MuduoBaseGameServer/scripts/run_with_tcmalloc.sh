#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo "Usage: $0 <game-server-bin> [server args...]" >&2
    echo "Optional: set TCMALLOC_LIB=/path/to/libtcmalloc_minimal.so.4" >&2
}

if [[ $# -lt 1 ]]; then
    usage
    exit 64
fi

server_bin="$1"
shift

if [[ ! -x "$server_bin" ]]; then
    echo "GameServer binary is not executable: $server_bin" >&2
    exit 66
fi

find_tcmalloc() {
    if [[ -n "${TCMALLOC_LIB:-}" ]]; then
        echo "$TCMALLOC_LIB"
        return 0
    fi

    local script_dir repo_root
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    repo_root="$(cd "$script_dir/../.." && pwd)"

    local candidates=(
        "/usr/lib/x86_64-linux-gnu/libtcmalloc_minimal.so.4"
        "/usr/local/lib/libtcmalloc_minimal.so.4"
        "$repo_root/Temp/tcmalloc-root/usr/lib/x86_64-linux-gnu/libtcmalloc_minimal.so.4"
    )

    local lib
    for lib in "${candidates[@]}"; do
        if [[ -r "$lib" ]]; then
            echo "$lib"
            return 0
        fi
    done

    return 1
}

tcmalloc_lib="$(find_tcmalloc || true)"
if [[ -z "$tcmalloc_lib" ]]; then
    echo "tcmalloc library not found." >&2
    echo "Install libtcmalloc-minimal4 or set TCMALLOC_LIB=/path/to/libtcmalloc_minimal.so.4." >&2
    exit 69
fi

export LD_PRELOAD="$tcmalloc_lib${LD_PRELOAD:+ $LD_PRELOAD}"
exec "$server_bin" "$@"
