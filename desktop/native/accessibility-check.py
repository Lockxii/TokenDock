"""Read the real OS accessibility tree of our synthetic app only, on disposable CI."""
import json, os, pathlib, subprocess, sys, tempfile, time
if os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated':
    raise SystemExit('Isolated CI required')
root = pathlib.Path(__file__).resolve().parent
mac = sys.platform == 'darwin'
if mac:
    probe = pathlib.Path('artifacts/accessibility-probe').resolve()
    subprocess.run(['swiftc', str(root / 'AccessibilityProbe.swift'), '-o', str(probe), '-framework', 'ApplicationServices'], check=True)
    result = subprocess.run([str(probe), '0', 'preflight'])
    if result.returncode: raise SystemExit(result.returncode)
else:
    import dbus
    bus = dbus.SessionBus()
    status = dbus.Interface(bus.get_object('org.a11y.Bus', '/org/a11y/bus'), 'org.freedesktop.DBus.Properties')
    status.Set('org.a11y.Status', 'IsEnabled', dbus.Boolean(True))
    import pyatspi
    from gi.repository import GLib

last_snapshot = []
def until(check, description):
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        value = check()
        if value: return value
        time.sleep(.15)
    print("Last synthetic accessibility snapshot:", json.dumps(last_snapshot[:100]))
    raise AssertionError(description)

with tempfile.TemporaryDirectory(prefix='tokendock-a11y-') as directory:
    directory = pathlib.Path(directory)
    (directory / 'fixture.marker').write_text('synthetic')
    (directory / 'module').write_text('all')
    target = pathlib.Path(sys.argv[1]).resolve()
    command = ['dotnet', str(target)] if target.suffix == '.dll' else [str(target)]
    app = subprocess.Popen(command + ['--accessibility-fixture', str(directory)])
    try:
        def tree(press=False, focus=None):
            global last_snapshot
            if mac:
                result = subprocess.run([str(probe), str(app.pid), 'focus:'+focus if focus else 'press-camera' if press else 'snapshot'], capture_output=True, text=True, timeout=15)
                if press: print(result.stderr, flush=True)
                if result.returncode: return []
                last_snapshot = json.loads(result.stdout)
                return last_snapshot
            desktop = pyatspi.Registry.getDesktop(0)
            target = next((a for a in desktop if a.get_process_id() == app.pid), None)
            if target is None: return []
            nodes = []
            def visit(node, depth=0):
                if depth >= 32 or len(nodes) >= 2000: return
                if focus and node.name == focus:
                    assert node.queryComponent().grabFocus(), "Native focus request refused"
                actions = []
                try:
                    action = node.queryAction()
                    actions = [action.getName(i) for i in range(action.nActions)]
                    if press and node.name == 'Camera': assert action.doAction(0)
                except NotImplementedError: pass
                nodes.append(dict(name=node.name, role=node.getRoleName(), actions=actions))
                for child in node:
                    if child: visit(child, depth+1)
            try:
                visit(target)
            except GLib.Error as error:
                # A module switch destroys old peers during enumeration. Retry
                # only the explicit stale-object error; persistent failures time out.
                if "No handler registered for object" not in str(error): raise
                return []
            last_snapshot = nodes
            return nodes
        def spoken(name):
            voiceover = os.environ.get('TOKENDOCK_VOICEOVER_DRIVER')
            if voiceover:
                until(lambda: tree(focus=name), f'Native focus request failed for {name}')
                last_phrase = []
                def announced_by_voiceover():
                    result = subprocess.run([voiceover,'read-focused'],capture_output=True,text=True,timeout=8)
                    phrase = (result.returncode, result.stdout.strip(), result.stderr.strip())
                    if not last_phrase or last_phrase[-1] != phrase:
                        print('VoiceOver diagnostic:', repr(phrase), flush=True); last_phrase.append(phrase)
                    return result.returncode == 0 and name in result.stdout
                until(announced_by_voiceover, f'VoiceOver did not announce {name}')
                print(f'PASS: VoiceOver actual last phrase contains {name}', flush=True)
                return
            log = os.environ.get('TOKENDOCK_ORCA_LOG')
            if not log: return
            path = pathlib.Path(log)
            before = path.stat().st_size if path.exists() else 0
            tree(focus=name)
            def announced():
                with path.open(errors='replace') as stream:
                    stream.seek(before)
                    lines = [line for line in stream if 'SPEECH OUTPUT:' in line]
                    assert not any(label in line for line in lines for label in ['Canvas panel', 'Border panel', 'StackPanel panel', 'ScrollContentPresenter panel']), 'Decorative layout names leaked into Orca speech'
                    return any(name in line for line in lines)
            until(announced, f'Orca did not announce {name}')
            print(f'PASS: Orca generated speech for {name}', flush=True)
        until(lambda: (directory/'ready').exists(), 'Fixture did not open')
        if os.environ.get('TOKENDOCK_ORCA_LOG'):
            windows = subprocess.check_output(['xdotool','search','--onlyvisible','--pid',str(app.pid)], text=True).splitlines()
            assert windows, 'Synthetic dock window absent'
            subprocess.run(['xdotool','windowfocus','--sync',windows[0]], check=True)
            spoken('Camera')
        if os.environ.get('TOKENDOCK_VOICEOVER_DRIVER'): spoken('Camera')
        until(lambda: any(n['name']=='Camera' for n in tree()), 'Camera action absent from native accessibility tree')
        initial = tree()
        camera = next(n for n in initial if n['name']=='Camera')
        print('Native Camera peer:', json.dumps(camera), flush=True)
        advertised = ('AXPress' in camera['actions']) if mac else bool(camera['actions'])
        tree(True)
        until(lambda: any(n['name']=='Take photo' for n in tree()), 'Native accessibility action did not open Camera')
        assert advertised, 'Camera is invokable but does not advertise native accessibility actions'
        expectations = {'music':['Playback position','Previous track','Play or pause','Next track'], 'clipboard':['Clear','Remove item'], 'camera':['Take photo'], 'settings':['Apply','Cancel','Featured token counter source']}
        for module, names in expectations.items():
            (directory/'module.next').write_text(module)
            (directory/'module.next').replace(directory/'module')
            until(lambda: (directory/'ready').read_text()==module, f'{module} did not open')
            nodes = until(lambda: (snapshot if all(any(n['name']==name for n in snapshot) for name in names) else None) if (snapshot:=tree()) else None, f'Missing accessible names in {module}: {names}')
            for name in names:
                node = next(n for n in nodes if n['name']==name)
                assert node['role'], f'Missing role for {name}'
            spoken(names[0])
            if module == 'music':
                assert any(n['name']=='Playback position' and 'slider' in n['role'].lower() for n in nodes)
        print('PASS: external native accessibility tree, names, roles and Camera activation; synthetic content only.')
    finally:
        (directory/'stop').write_text('stop')
        try: app.wait(timeout=5)
        except subprocess.TimeoutExpired:
            app.kill(); app.wait(timeout=5)
        if app.returncode != 0: raise AssertionError(f'Accessibility fixture exit: {app.returncode}')
