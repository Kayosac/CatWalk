"""Bounded commands for the Fuel 04 authoring tool in the already open Unity Editor."""
import argparse
import json
import pathlib
import time
import uuid

root = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['build', 'validate', 'preview', 'audit'])
parser.add_argument('--timeout', type=int, default=90)
args = parser.parse_args()
source = root / 'CatWalk/Assets/SpaceCourier/Editor/FuelStop04Authoring.cs'
assembly = root / 'CatWalk/Library/ScriptAssemblies/Assembly-CSharp-Editor.dll'
compile_deadline = time.monotonic() + min(45, args.timeout)
while not assembly.exists() or assembly.stat().st_mtime < source.stat().st_mtime:
    if time.monotonic() >= compile_deadline:
        raise SystemExit('Fuel 04 authoring code has not compiled; refresh Unity and inspect compiler errors.')
    time.sleep(.25)
queue = root / 'CatWalk/Temp/fuel04-command.json'
if queue.exists():
    raise SystemExit('A Fuel 04 command is already pending in Unity.')
request = {'id': str(uuid.uuid4()), 'action': args.action}
temp = queue.with_suffix('.tmp')
temp.write_text(json.dumps(request)); temp.replace(queue)
result_file = root / 'Builds/FuelStop04/editor-result.json'
deadline = time.monotonic() + args.timeout
while time.monotonic() < deadline:
    if result_file.exists():
        result = json.loads(result_file.read_text())
        if result.get('id') == request['id']:
            checks = result.pop('checks', [])
            result['check_count'] = len(checks or [])
            print(json.dumps(result, ensure_ascii=False, indent=2))
            raise SystemExit(0 if result.get('success') else 1)
    time.sleep(.25)
raise SystemExit('Fuel 04 command was not acknowledged. Inspect Unity compile errors; the pending request remains in Temp.')
