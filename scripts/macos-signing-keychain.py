"""Short-lived signing keychain. Never print subprocess arguments or secrets."""
import base64, json, os, pathlib, re, secrets, shlex, shutil, subprocess, sys

def command(args, label):
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode: raise RuntimeError(label + ' failed (details suppressed to protect credentials)')
    return result.stdout

def location():
    root = pathlib.Path(os.environ['RUNNER_TEMP'])
    if not root.is_absolute(): raise RuntimeError('Absolute runner temporary directory required')
    return root.resolve() / 'tokendock-signing'

def cleanup():
    folder = location()
    if not folder.exists(): return
    if not (folder/'owner').is_file(): raise RuntimeError('Refusing to clean an unowned directory')
    failure = None
    old = folder/'search-list.json'
    try:
        if old.exists(): command(['security','list-keychains','-d','user','-s',*json.loads(old.read_text())], 'Restore keychain search list')
    except Exception as error: failure = error
    keychain = folder/'signing.keychain-db'
    try:
        if keychain.exists(): command(['security','delete-keychain',str(keychain)], 'Delete temporary keychain')
    except Exception as error: failure = error
    finally: shutil.rmtree(folder)
    if failure: raise RuntimeError('Keychain cleanup failed; runner teardown is required') from None

def decode(name, path):
    value = os.environ.get(name, '')
    if not value or len(value) > 24*1024*1024: raise RuntimeError('Missing or oversized '+name)
    try: data = base64.b64decode(''.join(value.split()), validate=True)
    except ValueError: raise RuntimeError('Invalid encoding for '+name) from None
    path.write_bytes(data); path.chmod(0o600)

def prepare():
    team = os.environ.get('APPLE_TEAM_ID','')
    if not re.fullmatch('[A-Z0-9]{10}',team): raise RuntimeError('Invalid Apple Team ID')
    for variable, kind in [('MACOS_APP_IDENTITY','Application'),('MACOS_INSTALLER_IDENTITY','Installer')]:
        identity = os.environ.get(variable,'')
        if not identity.startswith('Developer ID '+kind+': ') or not identity.endswith('('+team+')') or any(c in identity for c in '\n\r\0'):
            raise RuntimeError('Invalid '+variable)
    folder = location(); folder.mkdir(mode=0o700)
    (folder/'owner').write_text('TokenDock CI signing')
    try:
        old = shlex.split(command(['security','list-keychains','-d','user'], 'Read keychain search list'))
        (folder/'search-list.json').write_text(json.dumps(old))
        keychain = str(folder/'signing.keychain-db'); password = secrets.token_urlsafe(32)
        command(['security','create-keychain','-p',password,keychain], 'Create keychain')
        command(['security','set-keychain-settings','-lut','7200',keychain], 'Configure keychain')
        command(['security','unlock-keychain','-p',password,keychain], 'Unlock keychain')
        for kind in ['APP','INSTALLER']:
            path = folder/(kind+'.p12'); decode('MACOS_'+kind+'_P12_BASE64',path)
            secret = os.environ.get('MACOS_'+kind+'_P12_PASSWORD')
            if not secret: raise RuntimeError('Missing export password')
            command(['security','import',str(path),'-k',keychain,'-P',secret,'-T','/usr/bin/codesign','-T','/usr/bin/pkgbuild','-T','/usr/bin/productsign'], 'Import '+kind+' identity')
            path.unlink()
        command(['security','set-key-partition-list','-S','apple-tool:,apple:','-s','-k',password,keychain], 'Limit signing key access')
        command(['security','list-keychains','-d','user','-s',keychain,*old], 'Set temporary search list')
        identities = command(['security','find-identity','-v',keychain], 'Verify signing identities')
        for name in ['MACOS_APP_IDENTITY','MACOS_INSTALLER_IDENTITY']:
            if '"'+os.environ[name]+'"' not in identities: raise RuntimeError('Certificate/private-key identity missing: '+name)
        args = ['xcrun','notarytool','store-credentials','TokenDock-CI','--keychain',keychain]
        if os.environ.get('APPLE_API_KEY_P8_BASE64'):
            path = folder/'notary.p8'; decode('APPLE_API_KEY_P8_BASE64',path)
            if not os.environ.get('APPLE_API_KEY_ID') or not os.environ.get('APPLE_API_ISSUER_ID'): raise RuntimeError('Missing API key metadata')
            args += ['--key',str(path),'--key-id',os.environ['APPLE_API_KEY_ID'],'--issuer',os.environ['APPLE_API_ISSUER_ID']]
        else:
            if not os.environ.get('APPLE_ID') or not os.environ.get('APPLE_APP_SPECIFIC_PASSWORD'): raise RuntimeError('Missing notarization credentials')
            args += ['--apple-id',os.environ['APPLE_ID'],'--team-id',team,'--password',os.environ['APPLE_APP_SPECIFIC_PASSWORD']]
        command(args, 'Validate and store notarization credentials')
        (folder/'notary.p8').unlink(missing_ok=True)
    except Exception:
        cleanup()
        raise

if __name__ == '__main__':
    if sys.platform != 'darwin' or os.environ.get('CI') != 'true' or os.environ.get('GITHUB_REF') != 'refs/heads/main':
        raise SystemExit('Trusted main-branch macOS CI required')
    try:
        if sys.argv[1:] == ['prepare']: prepare()
        elif sys.argv[1:] == ['cleanup']: cleanup()
        else: raise RuntimeError('Expected prepare or cleanup')
    except Exception as error:
        print(str(error) if isinstance(error,RuntimeError) else 'Signing keychain operation failed; no credentials logged',file=sys.stderr)
        raise SystemExit(1)
