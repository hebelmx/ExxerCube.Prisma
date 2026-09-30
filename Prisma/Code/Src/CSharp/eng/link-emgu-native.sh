#!/usr/bin/env bash
# Linux-native shim for Emgu.CV's libcvextern.so.
#
# The distro-pinned Emgu.CV.runtime.ubuntu-26.04-x64 package ships its native under
# runtimes/ubuntu-x64/native/libcvextern.so and deps.json keys it to rid "ubuntu-x64".
# But the .NET host runs as the portable "linux-x64" RID, and the RID fallback graph only
# resolves specific->general, so a linux-x64 host never looks inside the more-specific
# ubuntu-x64 folder. The native is therefore present in output but unreachable.
#
# The default DllImport loader DOES probe the app base dir, so symlink the native there.
# Idempotent; no-op when the native isn't in this project's output. NOTE: libcvextern.so
# still needs its system deps installed (VTK 9.5 / HDF5 310 / libavif16 / libgeotiff5 /
# liblapack3 on Ubuntu 26.04) or it loads but fails on a missing dependency.
#
# Usage: link-emgu-native.sh <TargetDir>
set -u
dir="${1:?usage: link-emgu-native.sh <TargetDir>}"
so=$(ls "${dir}"runtimes/*/native/libcvextern.so 2>/dev/null | head -1)
[ -n "$so" ] && ln -sf "$so" "${dir}libcvextern.so"
exit 0
