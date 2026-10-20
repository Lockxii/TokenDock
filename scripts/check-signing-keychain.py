"""Credential-free tests of secret handling, validation and cleanup failure paths."""
import base64, contextlib, importlib.util, io, os, pathlib, tempfile, unittest
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('keychain',pathlib.Path(__file__).with_name('macos-signing-keychain.py'))
m=importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.env={'RUNNER_TEMP':self.temp.name,'APPLE_TEAM_ID':'ABCDEF1234','MACOS_APP_IDENTITY':'Developer ID Application: Fixture (ABCDEF1234)','MACOS_INSTALLER_IDENTITY':'Developer ID Installer: Fixture (ABCDEF1234)','MACOS_APP_P12_BASE64':base64.b64encode(b'fixture').decode(),'MACOS_INSTALLER_P12_BASE64':base64.b64encode(b'fixture').decode(),'MACOS_APP_P12_PASSWORD':'synthetic-secret','MACOS_INSTALLER_P12_PASSWORD':'synthetic-secret','APPLE_ID':'fixture@example.invalid','APPLE_APP_SPECIFIC_PASSWORD':'synthetic-notary-secret'}
        self.scope=patch.dict(os.environ,self.env,clear=True);self.scope.start();self.calls=[]
    def tearDown(self): self.scope.stop();self.temp.cleanup()
    def fake(self,args,label):
        self.calls.append((args,label))
        if label=='Read keychain search list':return '"/fixture/login.keychain-db"'
        if label=='Create keychain':pathlib.Path(args[-1]).touch()
        if label=='Verify signing identities':return '\n'.join('"'+self.env[n]+'"' for n in ['MACOS_APP_IDENTITY','MACOS_INSTALLER_IDENTITY'])
        return ''
    def test_success_and_cleanup(self):
        with patch.object(m,'command',self.fake):
            m.prepare();self.assertFalse(list(m.location().glob('*.p12')));m.cleanup();m.cleanup()
        self.assertFalse(m.location().exists())
        self.assertTrue(any(label=='Restore keychain search list' for _,label in self.calls))
    def test_failure_removes_keys(self):
        def fail(args,label):
            if label=='Import INSTALLER identity':raise RuntimeError('Import failed')
            return self.fake(args,label)
        with patch.object(m,'command',fail):
            with self.assertRaises(RuntimeError):m.prepare()
        self.assertFalse(m.location().exists())
    def test_restore_failure_still_deletes_keychain(self):
        with patch.object(m,'command',self.fake):m.prepare()
        def fail(args,label):
            if label=='Restore keychain search list':raise RuntimeError('Restore failed')
            return self.fake(args,label)
        with patch.object(m,'command',fail):
            with self.assertRaises(RuntimeError):m.cleanup()
        self.assertTrue(any(label=='Delete temporary keychain' for _,label in self.calls));self.assertFalse(m.location().exists())
    def test_reject_wrong_team_before_creating_files(self):
        os.environ['APPLE_TEAM_ID']='WRONGTEAM1'
        with self.assertRaises(RuntimeError):m.prepare()
        self.assertFalse(m.location().exists())
    def test_no_subprocess_secret_in_errors(self):
        with patch.object(m.subprocess,'run',return_value=type('Result',(),{'returncode':1,'stdout':'','stderr':'synthetic-secret'})()):
            with self.assertRaises(RuntimeError) as result:m.command(['security','synthetic-secret'],'Import')
        self.assertNotIn('synthetic-secret',str(result.exception))
    def test_unowned_directory_preserved(self):
        m.location().mkdir()
        with self.assertRaises(RuntimeError):m.cleanup()
        self.assertTrue(m.location().exists())
unittest.main()
