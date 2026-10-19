"""Real X11 pointer/Xdnd round trips, restricted to a disposable Xvfb CI display."""
import os, pathlib, subprocess, sys, tempfile, time, struct, zlib
if os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated' or sys.platform != 'linux':
    raise SystemExit('Native drag checks require isolated Linux CI.')
def xdo(*args):
    return subprocess.check_output(['xdotool', *map(str,args)], text=True).strip()
def drag(x1,y1,x2,y2):
    xdo('mousemove','--sync',x1,y1)
    xdo('mousedown',1)
    for i in range(1,21):
        xdo('mousemove','--sync',round(x1+(x2-x1)*i/20),round(y1+(y2-y1)*i/20))
        time.sleep(.025)
    time.sleep(.2)
    xdo('mouseup',1)
def chunk(kind,data):
    return struct.pack('!I',len(data))+kind+data+struct.pack('!I',zlib.crc32(kind+data)&0xffffffff)
with tempfile.TemporaryDirectory(prefix='tokendock-drag-') as temp:
    root=pathlib.Path(temp)
    (root/'fixture.marker').write_text('synthetic')
    (root/'space # café.txt').write_text('Synthetic drag fixture',encoding='utf-8')
    pixels=b''.join(b'\0'+b''.join(bytes((30,190,130,255 if (x-16)**2+(y-16)**2<225 else 0)) for x in range(32)) for y in range(32))
    (root/'round.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',32,32,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(pixels))+chunk(b'IEND',b''))
    processes=[subprocess.Popen(['dotnet',sys.argv[1],'--native-drag-check',role,str(root)]) for role in ('source','sink')]
    def wait_for(check):
        deadline=time.monotonic()+15
        while time.monotonic()<deadline:
            if (root/'failure').exists(): raise RuntimeError((root/'failure').read_text())
            if any(p.poll() is not None for p in processes): raise RuntimeError('Fixture process exited unexpectedly')
            if check(): return
            time.sleep(.1)
        raise TimeoutError('Native drag fixture did not receive the expected data')
    try:
        wait_for(lambda: all((root/(role+'.ready')).exists() for role in ('source','sink')))
        for count,y in enumerate((102,158),1):
            drag(200,y,610,230)
            wait_for(lambda: (root/'sink.events').exists() and len((root/'sink.events').read_text().splitlines())==count)
            drag(600,102,210,230)
            wait_for(lambda: (root/'source.events').exists() and len((root/'source.events').read_text().splitlines())==count)
        assert (root/'space # café.txt').read_text(encoding='utf-8')=='Synthetic drag fixture'
        print('PASS: native cross-process file/photo drag into shelf and back, Unicode URLs, original bytes preserved.')
    finally:
        for role,p in zip(('source','sink'),processes):
            try:
                win=xdo('search','--name','^TokenDock drag '+role+'$').splitlines()[0]
                xdo('windowfocus','--sync',win)
                xdo('key','Escape')
                p.wait(timeout=5)
            except (subprocess.SubprocessError,IndexError):
                p.terminate()
                try: p.wait(timeout=5)
                except subprocess.TimeoutExpired: p.kill();p.wait()
