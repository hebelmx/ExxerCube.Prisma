#!/usr/bin/env bash
# Linux-native shim for the Tesseract.NET (charlesw) wrapper.
#
# The Tesseract NuGet ships only Windows natives (x64/tesseract50.dll,
# x64/leptonica-1.82.0.dll) and on Linux P/Invokes the sonames
# `libtesseract50.so`, `libleptonica-1.82.0.so` and `libdl.so`. Modern distros
# (Ubuntu 24.04+/26.04) ship different soversions (libtesseract.so.5,
# libleptonica.so.6) and folded libdl into libc (only libdl.so.2 remains), so the
# wrapper throws DllNotFoundException even after `apt install tesseract-ocr`.
#
# This script symlinks the ABI-compatible system libs to the names the wrapper's
# loader searches for: x64/ for leptonica+tesseract, the base dir for the libdl
# P/Invoke. Sonames are resolved via ldconfig so it adapts to the host. Idempotent.
#
# Usage: link-tesseract-natives.sh <TargetDir>   (TargetDir = the build output dir)
set -u
dir="${1:?usage: link-tesseract-natives.sh <TargetDir>}"
[ -f "${dir}/x64/leptonica-1.82.0.dll" ] || exit 0   # not a Tesseract-consuming project

soname() { ldconfig -p | awk -v re="$1" '$0 ~ re {print $NF; exit}'; }
# Match BOTH leptonica sonames seen across distros: the older `liblept.so.5`
# (Ubuntu 24.04/noble — what dotnet/aspnet:10.0 ships) and the newer
# `libleptonica.so.6`. The bare stem `liblept` matches both; the previous
# `libleptonica\.so` regex silently missed `liblept.so.5`, leaving the wrapper
# without a leptonica symlink and throwing DllNotFoundException at OCR time.
lept=$(soname 'liblept')
tess=$(soname 'libtesseract\.so')
dl=$(soname 'libdl\.so\.2')

mkdir -p "${dir}/x64"
[ -n "$lept" ] && ln -sf "$lept" "${dir}/x64/libleptonica-1.82.0.so"
[ -n "$tess" ] && ln -sf "$tess" "${dir}/x64/libtesseract50.so"
[ -n "$dl" ]   && ln -sf "$dl"   "${dir}/libdl.so"
exit 0
