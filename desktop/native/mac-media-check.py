"""Exercise the real MediaRemote boundary on a disposable hosted desktop only."""
import os, pathlib, plistlib, subprocess, sys
if sys.platform != 'darwin' or os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated':
    raise SystemExit('This test requires an isolated macOS CI session.')
root = pathlib.Path(__file__).resolve().parent
bundle = pathlib.Path('artifacts/TokenDockMediaFixture.app').resolve()
binary = bundle / 'Contents/MacOS/fixture'
binary.parent.mkdir(parents=True, exist_ok=True)
with (bundle / 'Contents/Info.plist').open('wb') as out:
    plistlib.dump(dict(CFBundleIdentifier='app.tokendock.mediafixture', CFBundleExecutable='fixture',
                      CFBundleName='TokenDock Media Fixture', CFBundlePackageType='APPL', LSUIElement=True), out)
subprocess.run(['swiftc', '-swift-version', '5', str(root / 'MediaFixture.swift'), '-o', str(binary),
                '-framework', 'AppKit', '-framework', 'MediaPlayer'], check=True)
subprocess.run(['codesign', '--force', '--sign', '-', str(bundle)], check=True)
process = subprocess.Popen([str(binary)])
try:
    subprocess.run(['dotnet', sys.argv[1], '--media-check'], check=True, timeout=90)
finally:
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)
