"""Real VoiceOver last-phrase smoke test; only on a disposable hosted desktop."""
import os, pathlib, subprocess, sys, time
if sys.platform!='darwin' or os.environ.get('CI')!='true' or os.environ.get('TOKENDOCK_TEST_DISPLAY')!='isolated':
    raise SystemExit('Isolated macOS CI required')
root=pathlib.Path(__file__).resolve().parent
helper=pathlib.Path('artifacts/voiceover-driver').resolve()
subprocess.run(['swiftc',str(root/'VoiceOverDriver.swift'),'-o',str(helper),'-framework','AppKit','-framework','ApplicationServices','-framework','Carbon'],check=True)
preflight=subprocess.run([str(helper),'preflight'])
if preflight.returncode: raise SystemExit(preflight.returncode)
try:
    started=subprocess.run([str(helper),'start'],timeout=60)
    if started.returncode: raise SystemExit(started.returncode)
    reference=subprocess.Popen([str(helper),'reference'])
    try:
        time.sleep(2)
        sample=subprocess.run([str(helper),'read-focused'],capture_output=True,text=True,timeout=15)
        print('Native reader reference:',sample.returncode,repr(sample.stdout),sample.stderr,flush=True)
        if sample.returncode == 77:
            print('UNVERIFIED: System Events keyboard permission unavailable'); raise SystemExit(77)
        if sample.returncode != 0 or 'TokenDock VoiceOver reference' not in sample.stdout:
            print('UNVERIFIED: VoiceOver cannot read the independent AppKit reference on this runner'); raise SystemExit(77)
        print('PASS: VoiceOver reads independent AppKit reference',flush=True)
    finally:
        reference.terminate()
        try: reference.wait(timeout=5)
        except subprocess.TimeoutExpired: reference.kill(); reference.wait()
    env=dict(os.environ,TOKENDOCK_VOICEOVER_DRIVER=str(helper))
    subprocess.run(['python3',str(root/'accessibility-check.py'),sys.argv[1]],env=env,check=True,timeout=180)
finally:
    subprocess.run([str(helper),'stop'],check=True,timeout=30)
