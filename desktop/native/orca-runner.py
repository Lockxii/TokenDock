"""Run the installed, unmodified Orca; flush its diagnostic stream for live assertions."""
import os, runpy, sys
if os.environ.get('CI')!='true' or os.environ.get('TOKENDOCK_TEST_DISPLAY')!='isolated':
    raise SystemExit('Isolated CI required')
from gi.repository import GLib
def flush_log():
    from orca import debug
    if debug.debugFile: debug.debugFile.flush()
    return True
GLib.timeout_add(100,flush_log)
sys.argv[0]='/usr/bin/orca'
runpy.run_path('/usr/bin/orca',run_name='__main__')
