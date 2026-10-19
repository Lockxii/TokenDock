"""Private D-Bus fixture. Run under dbus-run-session; never touches desktop players."""
import asyncio
import sys
from dbus_next.aio import MessageBus
from dbus_next import Variant
from dbus_next.service import ServiceInterface, method, dbus_property
from dbus_next.constants import PropertyAccess

class Player(ServiceInterface):
    def __init__(self):
        super().__init__('org.mpris.MediaPlayer2.Player')
        self.playing = True
        self.position = 12_000_000

    @dbus_property(access=PropertyAccess.READ)
    def PlaybackStatus(self) -> 's':
        return 'Playing' if self.playing else 'Paused'

    @dbus_property(access=PropertyAccess.READ)
    def Metadata(self) -> 'a{sv}':
        return {'mpris:trackid': Variant('o', '/fixture/track1'),
                'mpris:length': Variant('x', 120_000_000),
                'xesam:title': Variant('s', 'Synthetic track'),
                'xesam:artist': Variant('as', ['Fixture artist'])}

    @dbus_property(access=PropertyAccess.READ)
    def Position(self) -> 'x':
        return self.position

    @dbus_property(access=PropertyAccess.READ)
    def CanControl(self) -> 'b': return True

    @dbus_property(access=PropertyAccess.READ)
    def CanPlay(self) -> 'b': return True

    @dbus_property(access=PropertyAccess.READ)
    def CanPause(self) -> 'b': return True

    @dbus_property(access=PropertyAccess.READ)
    def CanSeek(self) -> 'b': return True

    @dbus_property(access=PropertyAccess.READ)
    def CanGoNext(self) -> 'b': return True

    @dbus_property(access=PropertyAccess.READ)
    def CanGoPrevious(self) -> 'b': return True

    @method()
    def PlayPause(self):
        self.playing = not self.playing
        self.emit_properties_changed({'PlaybackStatus': self.PlaybackStatus})

    @method()
    def SetPosition(self, track: 'o', position: 'x'):
        self.position = position

    @method()
    def Seek(self, offset: 'x'):
        self.position += offset

    @method()
    def Next(self): self.position = 0

    @method()
    def Previous(self): self.position = 0

async def main():
    bus = await MessageBus().connect()
    bus.export('/org/mpris/MediaPlayer2', Player())
    await bus.request_name('org.mpris.MediaPlayer2.TokenDockFixture')
    process = await asyncio.create_subprocess_exec('dotnet', sys.argv[1], '--media-check')
    try:
        return await asyncio.wait_for(process.wait(), 30)
    finally:
        if process.returncode is None:
            process.kill()
            await process.wait()
        bus.disconnect()

raise SystemExit(asyncio.run(main()))
