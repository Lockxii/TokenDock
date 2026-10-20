"""Real macOS sandbox file denial against a synthetic shelf import only."""
import json, os, pathlib, subprocess, sys, tempfile
if sys.platform!='darwin' or os.environ.get('CI')!='true' or os.environ.get('TOKENDOCK_TEST_DISPLAY')!='isolated':
    raise SystemExit('Isolated macOS CI required')
with tempfile.TemporaryDirectory(prefix='tokendock-permission-') as folder:
    root=pathlib.Path(folder).resolve(); original=root/'denied.txt'
    original.write_text('synthetic file; original must survive')
    (root/'fixture.marker').touch()
    profile='(version 1)(allow default)(deny file-read-data (literal '+json.dumps(str(original))+'))'
    target=pathlib.Path(sys.argv[1]).resolve()
    command=['dotnet',str(target)] if target.suffix=='.dll' else [str(target)]
    subprocess.run(['/usr/bin/sandbox-exec','-p',profile,*command,'--denied-shelf-check',str(original)],check=True,timeout=20)
    assert original.read_text()=='synthetic file; original must survive'
    print('PASS: original preserved after real OS-denied import; TCC was not modified.')
