#!/usr/bin/env bash
# Usage: summarize-trx.sh <notice title> <results directory>
# Prints a GitHub notice with the pass/fail counts from the first TRX file in the folder.
# Never fails the job: missing or empty results become a "no results" notice.
title=$1
results=$2

trx=$( { find "$results" -name '*.trx' 2>/dev/null || true; } | head -n 1)
if [ -z "$trx" ]; then
  echo "::notice title=$title::no test results (the build failed before tests ran)"
  exit 0
fi

counters=$( { grep -o '<Counters [^>]*>' "$trx" | head -n 1; } || true)
if [ -z "$counters" ]; then
  echo "::notice title=$title::no results"
  exit 0
fi

attr() { sed -n "s/.* $1=\"\([0-9]*\)\".*/\1/p" <<<"$counters"; }
echo "::notice title=$title::$(attr passed)/$(attr total) passed, $(attr failed) failed"
