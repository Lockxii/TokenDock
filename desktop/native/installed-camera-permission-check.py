"""Test consent of the CI-installed, ad-hoc hardened helper via LaunchServices."""
import json, os, pathlib, subprocess, sys, tempfile
if sys.platform!='darwin' or os.environ.get('CI')!='true' or os.environ.get('TOKENDOCK_TEST_DISPLAY')!='isolated':
    raise SystemExit('Isolated macOS CI required')
root=pathlib.Path(__file__).resolve().parent
launcher=pathlib.Path('artifacts/camera-launch').resolve()
subprocess.run(['swiftc',str(root/'CameraLaunch.swift'),'-o',str(launcher)],check=True)
driver=pathlib.Path('artifacts/camera-consent-driver').resolve()
subprocess.run(['swiftc',str(root/'CameraConsentDriver.swift'),'-o',str(driver)],check=True)
bundle='/Applications/TokenDock.app/Contents/Helpers/TokenDockCamera.app'
with tempfile.TemporaryDirectory(prefix='tokendock-consent-') as directory:
    directory=pathlib.Path(directory); (directory/'fixture.marker').write_text('synthetic')
    report=directory/'request.json'
    request=subprocess.Popen([str(launcher),bundle,'--permission-request-check',str(report)])
    try:
        dialog=subprocess.run([str(driver)],capture_output=True,text=True,timeout=23)
        print(dialog.stdout,flush=True)
        if dialog.returncode not in (0,77): raise RuntimeError(dialog.stderr)
        assert request.wait(timeout=40)==0, 'Installed helper launch failed'
    finally:
        if request.poll() is None: request.terminate(); request.wait(timeout=5)
    assert report.exists(), 'Installed helper did not produce permission evidence'
    result=json.loads(report.read_text())
    result['launch']='LaunchServices, installed ad-hoc hardened helper'
    result['realDialogDenied']=dialog.returncode==0
    result['responsibleIdentity']='hosted-compute-agent' if 'hosted-compute-agent' in dialog.stdout else 'TokenDock' if dialog.returncode==0 else 'not-observed'
    if result.get('status')!='UNVERIFIED':
        assert result['after']==2 and result['granted'] is False and result['devicesOpened'] is False, result
        status=directory/'status.json'
        subprocess.run([str(launcher),bundle,'--permission-status-check',str(status)],check=True,timeout=40)
        assert json.loads(status.read_text())['after']==2, 'Decision not preserved across launches'
        if result['responsibleIdentity']=='TokenDock':
            result['status']='PASS: installed helper consent denied and persisted; no device opened'
        else:
            result['status']='UNVERIFIED'; result['reason']='Installed helper identity not established by dialog'
    print(result,flush=True)
    pathlib.Path('artifacts/camera-consent-installed.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    if result['status']=='UNVERIFIED': print('::warning::Installed bundle camera consent remains UNVERIFIED')
