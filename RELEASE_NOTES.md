First release.

A Playnite plugin for playing visual novels: take screenshots and write notes
without leaving the game, and sync save files between machines.

**Screenshots and notes**

- A floating bubble appears while a game is running. It never takes focus, so
  the game stays in front.
- Press 📷 once to open a text box, type a note, press it again to send. The
  screenshot and the note go to Trilium together. Type nothing and it is just
  two clicks for a plain screenshot.
- 📝 works the same way but writes to the "translation problems" sub-note.
- Right-click 📷 copies the screenshot to the clipboard only.
- Optional global hotkey (Shift+F12 by default).

**Trilium**

Entries are appended under today's journal note, one note per game, with a
sub-note for translation problems:

```
2026-08-30
 └ <game name> 遊戲心得
     └ 翻譯問題
```

Only the server URL and an ETAPI token are needed. The day note comes from
Trilium's own calendar, so it fits an existing journal without extra setup.

**Save sync**

Optional. Uses rclone against any remote. Decides pull/push/conflict before
the game starts, pushes after it exits, and retries a missed push on the next
launch. Conflicts always ask; nothing is overwritten silently.

**Also**

- Locale Emulator batch conversion from the game context menu.
- Everything is configured in Playnite's add-on settings.
