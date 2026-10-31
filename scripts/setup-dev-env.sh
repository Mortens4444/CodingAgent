#!/usr/bin/env bash
# Installs the toolchains the coding agent uses to build, test and validate projects (Linux and macOS).
#
# Uses apt, dnf, pacman or Homebrew, whichever is found. Tools already on PATH are skipped.
# After installing, open a new terminal so the updated PATH is picked up (or use check_environment).
#
# Usage:
#   ./scripts/setup-dev-env.sh [--dry-run] [--include-optional] [Language ...]
#   Languages: All CSharp Python C Cpp Java JavaScript TypeScript Html Assembly Pascal Lisp (default: All)
#
# Examples:
#   ./scripts/setup-dev-env.sh C Cpp JavaScript
#   ./scripts/setup-dev-env.sh --dry-run All

set -euo pipefail

# Keep in sync with Services/DevEnvironment/ToolchainCatalog.cs and setup-dev-env.ps1.
all_languages="CSharp Python C Cpp Java JavaScript TypeScript Html Assembly Pascal Lisp"
optional_tools="make cmake"

dry_run=0
include_optional=0
languages=()

for arg in "$@"; do
    case "$arg" in
        --dry-run|-n) dry_run=1 ;;
        --include-optional) include_optional=1 ;;
        -h|--help) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        All|all) languages=($all_languages) ;;
        CSharp|Python|C|Cpp|Java|JavaScript|TypeScript|Html|Assembly|Pascal|Lisp) languages+=("$arg") ;;
        *) echo "Unknown argument: $arg (use --help)" >&2; exit 2 ;;
    esac
done

[ ${#languages[@]} -eq 0 ] && languages=($all_languages)

language_tools() {
    case "$1" in
        CSharp) echo "dotnet" ;;
        Python) echo "python" ;;
        C|Cpp) echo "gcc make cmake" ;;
        Java) echo "jdk" ;;
        JavaScript|TypeScript|Html) echo "node" ;;
        Assembly) echo "nasm gcc" ;;
        Pascal) echo "fpc" ;;
        Lisp) echo "sbcl" ;;
    esac
}

# Executables that satisfy a tool; any one of them on PATH is enough.
tool_commands() {
    case "$1" in
        dotnet) echo "dotnet" ;;
        python) echo "python3 python" ;;
        gcc) echo "gcc" ;;
        make) echo "make" ;;
        cmake) echo "cmake" ;;
        jdk) echo "javac" ;;
        node) echo "node" ;;
        nasm) echo "nasm" ;;
        fpc) echo "fpc" ;;
        sbcl) echo "sbcl" ;;
    esac
}

if command -v apt-get >/dev/null 2>&1; then
    manager=apt
elif command -v dnf >/dev/null 2>&1; then
    manager=dnf
elif command -v pacman >/dev/null 2>&1; then
    manager=pacman
elif command -v brew >/dev/null 2>&1; then
    manager=brew
else
    echo "No supported package manager (apt, dnf, pacman, brew) was found. Install the tools manually (see docs/DEV_ENVIRONMENT.md)." >&2
    exit 1
fi

# Package names per manager. "xcode" means the Xcode command line tools on macOS.
packages() {
    case "$manager:$1" in
        apt:dotnet) echo "dotnet-sdk-10.0" ;;
        apt:python) echo "python3 python3-pip" ;;
        apt:gcc) echo "build-essential" ;;
        apt:make) echo "make" ;;
        apt:cmake) echo "cmake" ;;
        apt:jdk) echo "openjdk-21-jdk" ;;
        apt:node) echo "nodejs npm" ;;
        dnf:dotnet) echo "dotnet-sdk-10.0" ;;
        dnf:python) echo "python3 python3-pip" ;;
        dnf:gcc) echo "gcc gcc-c++" ;;
        dnf:jdk) echo "java-21-openjdk-devel" ;;
        dnf:node) echo "nodejs npm" ;;
        pacman:dotnet) echo "dotnet-sdk" ;;
        pacman:python) echo "python python-pip" ;;
        pacman:gcc) echo "gcc" ;;
        pacman:jdk) echo "jdk21-openjdk" ;;
        pacman:node) echo "nodejs npm" ;;
        brew:dotnet) echo "--cask dotnet-sdk" ;;
        brew:python) echo "python" ;;
        brew:gcc|brew:make) echo "xcode" ;;
        brew:jdk) echo "openjdk@21" ;;
        brew:node) echo "node" ;;
        *) echo "$1" ;;
    esac
}

sudo_cmd=""
if [ "$manager" != brew ] && [ "$(id -u)" -ne 0 ]; then
    sudo_cmd="sudo"
fi

install() {
    case "$manager" in
        apt) run $sudo_cmd apt-get install -y "$@" ;;
        dnf) run $sudo_cmd dnf install -y "$@" ;;
        pacman) run $sudo_cmd pacman -S --needed --noconfirm "$@" ;;
        brew) run brew install "$@" ;;
    esac
}

run() {
    if [ $dry_run -eq 1 ]; then
        echo "  would run: $*"
    else
        "$@"
    fi
}

wanted=$(for language in "${languages[@]}"; do language_tools "$language"; done | tr ' ' '\n' | sort -u)

installed=()
apt_updated=0
for tool in $wanted; do
    if [ $include_optional -eq 0 ] && [[ " $optional_tools " == *" $tool "* ]]; then
        echo "skip     $tool (optional; use --include-optional)"
        continue
    fi

    found=""
    for command in $(tool_commands "$tool"); do
        if command -v "$command" >/dev/null 2>&1; then found=$(command -v "$command"); break; fi
    done
    if [ -n "$found" ]; then
        echo "present  $tool ($found)"
        continue
    fi

    package=$(packages "$tool")
    echo "install  $tool -> $package ($manager)"

    if [ "$package" = xcode ]; then
        run xcode-select --install || echo "warning: xcode-select --install failed" >&2
        installed+=("$tool")
        continue
    fi

    if [ "$manager" = apt ] && [ $apt_updated -eq 0 ]; then
        run $sudo_cmd apt-get update
        apt_updated=1
    fi

    # Package names are split on purpose ("nodejs npm", "--cask dotnet-sdk").
    # shellcheck disable=SC2086
    if install $package; then
        installed+=("$tool")
    else
        echo "warning: installing $package failed" >&2
    fi
done

echo
echo "Not installed by this script (per project):"
echo "  Maven/Gradle  use the project wrapper (mvnw / gradlew), or install maven / gradle with $manager"
echo "  JUnit         plain Java projects: put junit-platform-console-standalone-*.jar into lib/"
echo "  TypeScript    npm install --save-dev typescript   (the agent falls back to npx -p typescript)"
echo "  htmlhint      npm install --save-dev htmlhint      (optional; a built-in tag check is used without it)"
echo "  pytest        python3 -m pip install pytest        (optional; unittest is used without it)"

if [ ${#installed[@]} -gt 0 ] && [ $dry_run -eq 0 ]; then
    echo
    echo "Installed: ${installed[*]}. Open a new terminal so PATH includes them."
fi
