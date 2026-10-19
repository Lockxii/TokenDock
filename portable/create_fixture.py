"""Generate synthetic input; never opens a user's tool database."""
import pathlib
import sqlite3
import sys

target = pathlib.Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
if target.exists():
    raise SystemExit("Refusing to overwrite an existing fixture")
with sqlite3.connect(target) as database:
    database.execute("CREATE TABLE fixture (id INTEGER PRIMARY KEY, name TEXT)")
    database.executemany("INSERT INTO fixture VALUES (?, ?)", [(1, "Synthetic α"), (2, "Synthetic β")])
