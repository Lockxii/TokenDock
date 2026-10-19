"""Real X11 pointer/Xdnd round trips, restricted to a disposable Xvfb CI display."""
import os, pathlib, subprocess, sys, tempfile, time, struct, zlib
if os.environ.get('CI') != 'true' or os.environ.get('TOKENDOCK_TEST_DISPLAY') != 'isolated' or sys.platform not in ('linux','darwin'):
    raise SystemExit('Native drag checks require isolated CI.')
mac = sys.platform == 'darwin'
helper = pathlib.Path('artifacts/drag-input').resolve()
if mac:
    subprocess.run(['swiftc','-swift-version','5',str(pathlib.Path(__file__).with_name('DragInput.swift')),'-o',str(helper)],check=True)
    result = subprocess.run([str(helper),'preflight'])
    if result.returncode == 77:
        print('UNVERIFIED: macOS runner denies event injection; no permission prompt or desktop interaction attempted.')
        raise SystemExit(77)
    result.check_returncode()
def xdo(*args):
    return subprocess.check_output(['xdotool', *map(str,args)], text=True).strip()
def event(action,x,y):
    if mac: subprocess.run([str(helper),action,str(x),str(y)],check=True)
    elif action in ('move','drag'): xdo('mousemove','--sync',x,y)
    else: xdo('mousedown' if action=='down' else 'mouseup',1)
def activate(x,y):
    if mac:
        event('move',x,y);event('down',x,y);event('up',x,y)
        time.sleep(.3)
def drag(x1,y1,x2,y2):
    event('move',x1,y1)
    event('down',x1,y1)
    for i in range(1,21):
        event('drag',round(x1+(x2-x1)*i/20),round(y1+(y2-y1)*i/20))
        time.sleep(.025)
    time.sleep(.2)
    event('up',x2,y2)
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
        positions=[tuple(map(int,(root/(role+'.ready')).read_text().split())) for role in ('source','sink')]
        (sx,sy),(tx,ty)=positions
        print("Fixture origins:",positions,flush=True)
        for count,y in enumerate((42,98),1):
            activate(sx+10,sy+10)
            drag(sx+140,sy+y,tx+150,ty+170)
            wait_for(lambda: (root/'sink.events').exists() and len((root/'sink.events').read_text().splitlines())==count)
            activate(tx+10,ty+10)
            drag(tx+140,ty+42,sx+150,sy+170)
            wait_for(lambda: (root/'source.events').exists() and len((root/'source.events').read_text().splitlines())==count)
        assert (root/'space # café.txt').read_text(encoding='utf-8')=='Synthetic drag fixture'
        print('PASS: native cross-process file/photo drag into shelf and back, Unicode URLs, original bytes preserved.')
    finally:
        for role,p in zip(('source','sink'),processes):
            if p.poll() is not None: continue
            (root/(role+'.stop')).write_text('stop')
            try: p.wait(timeout=5)
            except subprocess.TimeoutExpired:
                p.terminate()
                try: p.wait(timeout=5)
                except subprocess.TimeoutExpired: p.kill();p.wait()
    assert all(p.returncode==0 for p in processes), 'Fixture did not shut down gracefully'

    if (root/'shelf.paths').exists():
        assert all(not pathlib.Path(p).exists() for p in (root/'shelf.paths').read_text().splitlines()), 'Shelf files survived graceful shutdown'
        print('PASS: native drag shelf cleanup after graceful process shutdown.')
