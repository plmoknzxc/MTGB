"""Run real Windows processes against the existing MTGB network transport."""
import argparse
import json
from pathlib import Path
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument("--mode", choices=["dedicated", "lan", "mismatch", "multiroom"], default="dedicated")
parser.add_argument("--build-dir", type=Path)
parser.add_argument("--best-of", type=int, choices=[1, 3], default=3)
parser.add_argument("--server-exe", type=Path)
parser.add_argument("--output-dir", type=Path, help="Separate evidence directory; defaults to the historical smoke path")
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
build = args.build_dir.resolve() if args.build_dir else root / "Builds/ForgeValidation"
output = args.output_dir.resolve() if args.output_dir else root / ".tools/forge-migration" / (args.mode + ("-bo1" if args.best_of == 1 else ""))
output.mkdir(parents=True, exist_ok=True)
roles = ["server", "client1", "client2"] if args.mode == "dedicated" else ["host", "client2"]
if args.mode == "mismatch":
    roles = ["server", "incompatible"]
if args.mode == "multiroom":
    roles = ["server", "client1", "client2", "client3", "client4"]
processes = []
port = "17877" if args.mode == "dedicated" else "17878"
try:
    for role in roles:
        report = output / (role + ".json")
        report.unlink(missing_ok=True)
        cmd = [str(build / "MTGB.exe"), "-batchmode", "-nographics", "-forge-smoke", role,
               "-forge-report", str(report), "-forge-port", port,
               "-forge-bestof", str(args.best_of),
               "-forge-room", "forge-" + args.mode,
               "-logFile", str(output / (role + ".log"))]
        if args.mode == "multiroom":
            cmd[cmd.index("-forge-room") + 1] = "forge-multi-b" if role in ("client3", "client4") else "forge-multi-a"
        process_directory = build
        if role == "server" and args.server_exe:
            executable = args.server_exe.resolve()
            cmd[0] = str(executable)
            process_directory = executable.parent
        process = subprocess.Popen(cmd, cwd=process_directory, creationflags=subprocess.CREATE_NO_WINDOW)
        processes.append((role, process))
        print("Started", role, process.pid, flush=True)
        if role in ("server", "host"):
            time.sleep(3)
    deadline = time.monotonic() + 150
    while time.monotonic() < deadline:
        clients = [(role, p) for role, p in processes if role != "server"]
        if all(p.poll() is not None for _, p in clients):
            break
        time.sleep(1)
    successful = True
    for role, process in processes:
        if role == "server":
            continue
        path = output / (role + ".json")
        result = json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else {"success": False, "detail": "No report"}
        print(role, json.dumps(result, ensure_ascii=False), flush=True)
        successful &= result["success"]
    if not successful:
        raise SystemExit(1)
finally:
    for role, process in processes:
        if process.poll() is None:
            subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True)
