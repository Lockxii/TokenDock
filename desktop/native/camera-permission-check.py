"""Exercise real AVFoundation consent without enumerating or opening a device."""
import json, os, pathlib, subprocess, sys
if sys.platform != 'darwin' or os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated':
    raise SystemExit('Isolated macOS CI required')
helper=pathlib.Path(sys.argv[1]).resolve()
if helper.name != 'tokendock-camera' or not helper.is_file(): raise SystemExit('Expected built camera helper')
result=subprocess.run([str(helper),'--permission-request-check'],capture_output=True,text=True,timeout=35)
evidence={'status':'UNVERIFIED','devicesOpened':False}
if result.returncode == 0:
    evidence=json.loads(result.stdout)
    assert evidence['requestCompleted'] and not evidence['devicesOpened']
    assert evidence['after'] in (1,2,3)
    # A second process must observe the same OS decision, not just a callback value.
    status=subprocess.check_output([str(helper),'--permission-status'],text=True,timeout=5).strip()
    assert status == {1:'restricted',2:'denied',3:'authorized'}[evidence['after']], status
    evidence['persistedStatus']=status
    evidence['status']='PASS: real authorization callback and cross-process status; no device opened'
    print(evidence['status'])
elif result.returncode == 77:
    evidence['reason']=result.stdout.strip()
    print('::warning::'+evidence['reason'])
else:
    raise RuntimeError('Camera consent probe failed: '+result.stderr)
pathlib.Path('artifacts/camera-consent.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8')
