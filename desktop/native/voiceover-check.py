"""Real VoiceOver last-phrase smoke test; only on a disposable hosted desktop."""
import os, pathlib, subprocess, sys
if sys.platform!='darwin' or os.environ.get('CI')!='true' or os.environ.get('TOKENDOCK_TEST_DISPLAY')!='isolated':
    raise SystemExit('Isolated macOS CI required')
root=pathlib.Path(__file__).resolve().parent
helper=pathlib.Path('artifacts/voiceover-driver').resolve()
subprocess.run(['swiftc',str(root/'VoiceOverDriver.swift'),'-o',str(helper),'-framework','AppKit','-framework','ApplicationServices','-framework','Carbon'],check=True)
preflight=subprocess.run([str(helper),'preflight'])
if preflight.returncode: raise SystemExit(preflight.returncode)
try:
    started=subprocess.run([str(helper),'start'])
    if started.returncode: raise SystemExit(started.returncode)
    env=dict(os.environ,TOKENDOCK_VOICEOVER_DRIVER=str(helper))
    subprocess.run(['python3',str(root/'accessibility-check.py'),sys.argv[1]],env=env,check=True,timeout=180)
finally:
    subprocess.run([str(helper),'stop'],check=True,timeout=15)
