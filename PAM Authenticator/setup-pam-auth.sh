#!/usr/bin/env bash
#
# Sets up SSH login with a JWT (via PAM_Authenticator) for ONE user.
#   1. /etc/pam.d/sshd       -> pam_succeed_if + pam_exec block
#   2. /etc/ssh/sshd_config  -> "Match User" block (keyboard-interactive only)
#
# Usage:
#   sudo ./setup-pam-auth.sh [--reload] [path-to-binary]   install / update
#   sudo ./setup-pam-auth.sh --remove [--reload]           roll back
#
# Optional env vars:
#   TARGET_USER   user that must authenticate with a JWT (default: fileodon)
#   PAM_FILE      PAM service file   (default: /etc/pam.d/sshd)
#   SSHD_FILE     sshd config file   (default: /etc/ssh/sshd_config)
#   PAM_PUBLIC_KEY_URL  public-key API endpoint
#                       (default: https://api.dev.fileodon.dev/api/organization/sftpconnection/pam/publickey)
#   PAM_PUBLIC_KEY_PATH  key destination (default: <AUTH_BIN directory>/public.pem)
#   PAM_PUBLIC_KEY_AUTH_HEADER  optional HTTP header for API authentication

set -euo pipefail

TARGET_USER="${TARGET_USER:-fileodon}"
PAM_FILE="${PAM_FILE:-/etc/pam.d/sshd}"
SSHD_FILE="${SSHD_FILE:-/etc/ssh/sshd_config}"
PAM_PUBLIC_KEY_URL="${PAM_PUBLIC_KEY_URL:-https://api.dev.fileodon.dev/api/organization/sftpconnection/pam/publickey}"
PAM_PUBLIC_KEY_AUTH_HEADER="${PAM_PUBLIC_KEY_AUTH_HEADER:-}"
BEGIN_MARK="# BEGIN PAM_Authenticator (${TARGET_USER})"
END_MARK="# END PAM_Authenticator (${TARGET_USER})"
STAMP="$(date +%Y%m%d-%H%M%S)"

die() { echo "Error: $*" >&2; exit 1; }

REMOVE=0
RELOAD=0
AUTH_BIN="/opt/pam-auth/PAM_Authenticator"
for arg in "$@"; do
    case "$arg" in
        --remove)  REMOVE=1 ;;
        --reload)  RELOAD=1 ;;
        -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
        -*)        die "unknown option: $arg" ;;
        *)         AUTH_BIN="$arg" ;;
    esac
done

[[ $EUID -eq 0 ]] || die "run as root (sudo)."
[[ -f "$PAM_FILE" ]]  || die "$PAM_FILE not found."
[[ -f "$SSHD_FILE" ]] || die "$SSHD_FILE not found."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# strip_block IN OUT: copy IN to OUT without this user's marked block
strip_block() {
    awk -v b="$BEGIN_MARK" -v e="$END_MARK" '
        $0 == b { skip = 1; next }
        $0 == e { skip = 0; next }
        !skip   { print }
    ' "$1" > "$2"
}

# backup FILE: prints the backup path
backup() {
    local bak="$1.bak.${STAMP}"
    cp -a "$1" "$bak"
    echo "$bak"
}

# insert_before PATTERN FILE BLOCKFILE: insert block before first matching line
insert_before() {
    local out="$WORK/insert.tmp"
    awk -v pat="$1" '
        FNR == NR { block = block $0 "\n"; next }
        !done && $0 ~ pat { printf "%s", block; done = 1 }
        { print }
        END { if (!done) exit 3 }
    ' "$3" "$2" > "$out" || return 1
    mv "$out" "$2"
}

PAM_NEW="$WORK/pam.new"
SSHD_NEW="$WORK/sshd.new"
strip_block "$PAM_FILE"  "$PAM_NEW"
strip_block "$SSHD_FILE" "$SSHD_NEW"

if [[ $REMOVE -eq 0 ]]; then
    [[ -x "$AUTH_BIN" ]] || echo "Warning: $AUTH_BIN does not exist or is not executable (yet)." >&2

    AUTH_DIR="$(dirname "$AUTH_BIN")"
    PUBLIC_KEY_PATH="${PAM_PUBLIC_KEY_PATH:-$AUTH_DIR/public.pem}"
    PUBLIC_KEY_DIR="$(dirname "$PUBLIC_KEY_PATH")"

    mkdir -p "$PUBLIC_KEY_DIR"
    PUBLIC_KEY_TMP="$WORK/public.pem"
    if command -v curl >/dev/null; then
        CURL_AUTH_ARGS=()
        [[ -z "$PAM_PUBLIC_KEY_AUTH_HEADER" ]] || CURL_AUTH_ARGS+=(--header "$PAM_PUBLIC_KEY_AUTH_HEADER")
        curl --fail --silent --show-error --location \
            --connect-timeout 10 --max-time 60 --retry 3 \
            "${CURL_AUTH_ARGS[@]}" --output "$PUBLIC_KEY_TMP" "$PAM_PUBLIC_KEY_URL" \
            || die "could not fetch the PAM public key from $PAM_PUBLIC_KEY_URL"
    elif command -v wget >/dev/null; then
        WGET_AUTH_ARGS=()
        [[ -z "$PAM_PUBLIC_KEY_AUTH_HEADER" ]] || WGET_AUTH_ARGS+=(--header="$PAM_PUBLIC_KEY_AUTH_HEADER")
        wget --quiet --timeout=60 --tries=3 \
            "${WGET_AUTH_ARGS[@]}" --output-document="$PUBLIC_KEY_TMP" "$PAM_PUBLIC_KEY_URL" \
            || die "could not fetch the PAM public key from $PAM_PUBLIC_KEY_URL"
    else
        die "curl or wget is required to fetch the PAM public key."
    fi

    [[ -s "$PUBLIC_KEY_TMP" ]] || die "PAM public-key endpoint returned an empty response."
    grep -Eq '-----BEGIN ([A-Z ]*)PUBLIC KEY-----' "$PUBLIC_KEY_TMP" \
        || die "PAM public-key endpoint did not return a PEM public key."

    chmod 0644 "$PUBLIC_KEY_TMP"
    mv -f "$PUBLIC_KEY_TMP" "$PUBLIC_KEY_PATH"
    echo "Installed PAM public key: $PUBLIC_KEY_PATH"

    PAM_BLOCK="$WORK/pam.block"
    SSHD_BLOCK="$WORK/sshd.block"

    cat > "$PAM_BLOCK" <<EOF
${BEGIN_MARK}
auth [success=ok default=1]     pam_succeed_if.so quiet user = ${TARGET_USER}
auth [success=done default=die] pam_exec.so expose_authtok quiet ${AUTH_BIN}
${END_MARK}
EOF

    # Match blocks run until the next Match/EOF, so this goes at the END of the file.
    cat > "$SSHD_BLOCK" <<EOF
${BEGIN_MARK}
Match User ${TARGET_USER}
    KbdInteractiveAuthentication yes
    AuthenticationMethods keyboard-interactive
${END_MARK}
EOF

    # PAM: before "@include common-auth", else before the first auth line
    # (e.g. RHEL-style "auth substack password-auth").
    insert_before '^[[:space:]]*@include[[:space:]]+common-auth' "$PAM_NEW" "$PAM_BLOCK" \
        || insert_before '^[[:space:]]*auth[[:space:]]' "$PAM_NEW" "$PAM_BLOCK" \
        || die "no 'common-auth' include or 'auth' line in $PAM_FILE; edit it manually."

    # sshd_config: make sure the file ends with a newline, then append
    [[ -z "$(tail -c1 "$SSHD_NEW")" ]] || echo >> "$SSHD_NEW"
    cat "$SSHD_BLOCK" >> "$SSHD_NEW"
fi

PAM_BAK="$(backup "$PAM_FILE")"
SSHD_BAK="$(backup "$SSHD_FILE")"
echo "Backups: $PAM_BAK, $SSHD_BAK"

# cat > keeps each original file's owner and permissions
cat "$PAM_NEW"  > "$PAM_FILE"
cat "$SSHD_NEW" > "$SSHD_FILE"

# Validate the sshd config; roll everything back if it is rejected
SSHD_BIN="$(command -v sshd || true)"
[[ -n "$SSHD_BIN" || ! -x /usr/sbin/sshd ]] || SSHD_BIN=/usr/sbin/sshd

if [[ -n "$SSHD_BIN" ]]; then
    if ! out="$("$SSHD_BIN" -t -f "$SSHD_FILE" 2>&1)"; then
        cat "$PAM_BAK"  > "$PAM_FILE"
        cat "$SSHD_BAK" > "$SSHD_FILE"
        die "sshd rejected the new config, restored both files from backup:
$out"
    fi
    echo "sshd -t: config is valid."

    if [[ $REMOVE -eq 0 ]]; then
        echo
        echo "Effective settings for ${TARGET_USER}:"
        "$SSHD_BIN" -T -f "$SSHD_FILE" -C "user=${TARGET_USER},host=localhost,addr=127.0.0.1" 2>/dev/null \
            | grep -Ei '^(usepam|kbdinteractiveauthentication|authenticationmethods) ' || true
        if "$SSHD_BIN" -T -f "$SSHD_FILE" 2>/dev/null | grep -qi '^usepam no'; then
            echo "WARNING: UsePAM is 'no'. Set 'UsePAM yes' globally in $SSHD_FILE, or PAM is never used." >&2
        fi
    fi
else
    echo "Warning: sshd binary not found, skipped config validation (run 'sshd -t' yourself)." >&2
fi

if [[ $RELOAD -eq 1 ]]; then
    if command -v systemctl >/dev/null; then
        systemctl reload ssh 2>/dev/null || systemctl reload sshd || die "could not reload the ssh service."
        echo "sshd reloaded."
    else
        echo "Warning: systemctl not found, reload sshd manually." >&2
    fi
fi

echo
if [[ $REMOVE -eq 1 ]]; then
    echo "Removed the PAM_Authenticator blocks for '${TARGET_USER}' from $PAM_FILE and $SSHD_FILE."
else
    echo "Installed PAM_Authenticator for '${TARGET_USER}' in $PAM_FILE and $SSHD_FILE."
    [[ $RELOAD -eq 1 ]] || echo "Apply with: sudo systemctl reload ssh   (or re-run with --reload)"
    echo "KEEP AN EXISTING SSH SESSION OPEN while testing so you can't lock yourself out."
    echo "To roll back: sudo $0 --remove --reload"
fi