"""Exercise real AVFoundation consent without enumerating or opening a device."""
import json, os, pathlib, subprocess, sys
if sys.platform != 'darwin' or os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated':
    raise SystemExit('Isolated macOS CI required')
helper=pathlib.Path(sys.argv[1]).resolve()
if helper.name != 'tokendock-camera' or not helper.is_file(): raise SystemExit('Expected built camera helper')
driver=pathlib.Path('artifacts/camera-consent-driver').resolve()
subprocess.run(['swiftc',str(pathlib.Path(__file__).with_name('CameraConsentDriver.swift')),'-o',str(driver)],check=True)
request=subprocess.Popen([str(helper),'--permission-request-check'],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
try:
    dialog=subprocess.run([str(driver)],capture_output=True,text=True,timeout=23)
    print(dialog.stdout,flush=True)
    if dialog.returncode not in (0,77): raise RuntimeError('Camera consent UI probe failed: '+dialog.stderr)
    stdout,stderr=request.communicate(timeout=30)
    result=subprocess.CompletedProcess(request.args,request.returncode,stdout,stderr)
finally:
    if request.poll() is None: request.kill(); request.wait()

evidence={'status':'UNVERIFIED','devicesOpened':False}
if result.returncode == 0:
    evidence=json.loads(result.stdout)
    evidence['realDialogDenied']=dialog.returncode == 0
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
