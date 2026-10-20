"""Real empty-keychain lifecycle on disposable CI, without certificates or accounts."""
import importlib.util, json, os, pathlib, secrets, shlex, sys
if sys.platform!='darwin' or os.environ.get('CI')!='true' or os.environ.get('TOKENDOCK_TEST_DISPLAY')!='isolated':
    raise SystemExit('Isolated macOS CI required')
spec=importlib.util.spec_from_file_location('keychain',pathlib.Path(__file__).with_name('macos-signing-keychain.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
folder=m.location();folder.mkdir(mode=0o700)
(folder/'owner').write_text('TokenDock CI signing')
original=shlex.split(m.command(['security','list-keychains','-d','user'],'Read search list'))
(folder/'search-list.json').write_text(json.dumps(original))
try:
    path=str(folder/'signing.keychain-db')
    m.command(['security','create-keychain','-p',secrets.token_urlsafe(32),path],'Create empty fixture keychain')
    m.command(['security','list-keychains','-d','user','-s',path,*original],'Set fixture search list')
finally:
    m.cleanup()
assert not folder.exists()
assert shlex.split(m.command(['security','list-keychains','-d','user'],'Verify restored list'))==original
print('PASS: real temporary keychain removed and original search list restored; no signing keys imported.')
