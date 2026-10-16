#!/bin/sh
set -eu

server_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$server_dir"

# Preserve the site's heap/CPU budget. These defaults match the small-server
# profile; the free-space policy returns idle heap without lowering its cap.
JAVA_TOOL_OPTIONS=${JAVA_TOOL_OPTIONS:--Xms32m -Xmx384m -XX:ActiveProcessorCount=2}
case " $JAVA_TOOL_OPTIONS ${JDK_JAVA_OPTIONS-} ${_JAVA_OPTIONS-} " in
    *HeapFreeRatio*|*ShrinkHeapInSteps*) ;;
    *) JAVA_TOOL_OPTIONS="$JAVA_TOOL_OPTIONS -XX:MinHeapFreeRatio=10 -XX:MaxHeapFreeRatio=20 -XX:-ShrinkHeapInSteps" ;;
esac
export JAVA_TOOL_OPTIONS

exec "$server_dir/MTGBServer" -batchmode -nographics -mtgb-server "$@"
