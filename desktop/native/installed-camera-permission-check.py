"""Installed ad-hoc bundle consent lifecycle; request-only mode never opens devices."""
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
    def status(label):
        report=directory/(label+'.json')
        subprocess.run([str(launcher),bundle,'--permission-status-check',str(report)],check=True,timeout=40)
        return json.loads(report.read_text())['after']
    def reset(label):
        # Only this CI-created app and its helper. Never reset Camera globally.
        for identifier in ('io.github.lockxii.tokendock','io.github.lockxii.tokendock.camera'):
            subprocess.run(['/usr/bin/tccutil','reset','Camera',identifier],check=True,timeout=10)
        assert status(label)==0, 'Scoped reset did not clear installed camera consent'
    def request(label,choice,expected):
        report=directory/(label+'.json')
        process=subprocess.Popen([str(launcher),bundle,'--permission-request-check',str(report)])
        try:
            dialog=subprocess.run([str(driver),choice],capture_output=True,text=True,timeout=23)
            print(dialog.stdout,flush=True)
            if dialog.returncode not in (0,77): raise RuntimeError(dialog.stderr)
            assert process.wait(timeout=40)==0, 'Installed helper launch failed'
        finally:
            if process.poll() is None: process.terminate(); process.wait(timeout=5)
        assert report.exists(), 'Installed helper did not produce permission evidence'
        result=json.loads(report.read_text())
        result['responsibleIdentity']='hosted-compute-agent' if 'hosted-compute-agent' in dialog.stdout else 'TokenDock' if dialog.returncode==0 else 'not-observed'
        result['dialogResponded']=dialog.returncode==0
        if result.get('status')=='UNVERIFIED' or result['responsibleIdentity']!='TokenDock':
            raise RuntimeError('Installed consent identity not validated: '+repr(result))
        assert result['after']==expected and result['granted']==(expected==3) and result['devicesOpened'] is False, result
        assert status(label+'-persisted')==expected, 'Decision not preserved across launches'
        return result
    results={'launch':'LaunchServices, installed ad-hoc hardened helper','devicesOpened':False}
    results['denied']=request('denied','deny',2)
    reset('reset-after-denial')
    try:
        results['granted']=request('granted','allow',3)
    finally:
        # Revoke the test grant even if the subsequent assertion fails.
        reset('reset-after-grant')
    results['deniedAgain']=request('denied-again','deny',2)
    results['status']='PASS: installed TokenDock consent deny, grant, scoped reset and deny again; decisions persist; no device opened'
    print(results,flush=True)
    pathlib.Path('artifacts/camera-consent-installed.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
