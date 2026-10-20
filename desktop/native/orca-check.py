"""Exercise real Orca output in a disposable Xvfb/D-Bus session, never user audio."""
import os, pathlib, subprocess, sys, tempfile, time
if sys.platform != 'linux' or os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated':
    raise SystemExit('Isolated Linux CI required')
with tempfile.TemporaryDirectory(prefix='tokendock-orca-') as temporary:
    env = dict(os.environ, XDG_CONFIG_HOME=temporary, LANG='en_US.UTF-8')
    env['TOKENDOCK_ORCA_LOG'] = str(pathlib.Path(temporary)/'orca.log')
    subprocess.run(['pulseaudio','--start','--exit-idle-time=-1'], env=env, check=True)
    orca = None
    try:
        subprocess.run(['pactl','load-module','module-null-sink','sink_name=tokendock_fixture'],env=env,check=True,stdout=subprocess.DEVNULL)
        subprocess.run(['pactl','set-default-sink','tokendock_fixture'],env=env,check=True)
        with open(pathlib.Path(temporary)/'process.log','w') as output:
            orca = subprocess.Popen(['orca','--replace','--debug','--debug-file='+env['TOKENDOCK_ORCA_LOG']],env=env,stdout=output,stderr=output)
            time.sleep(3)
            if orca.poll() is not None: raise RuntimeError('Orca did not remain running')
            subprocess.run(['/usr/bin/python3',str(pathlib.Path(__file__).with_name('accessibility-check.py')),sys.argv[1]],env=env,check=True,timeout=150)
    except Exception:
        for name in ['process.log','orca.log']:
            path=pathlib.Path(temporary)/name
            if path.exists():
                lines=path.read_text(errors='replace').splitlines()
                print(name, '\n'.join(lines[-40:]))
        raise
    finally:
        if orca is not None:
            orca.terminate()
            try: orca.wait(timeout=5)
            except subprocess.TimeoutExpired: orca.kill(); orca.wait()
        subprocess.run(['pulseaudio','--kill'],env=env,check=False)
