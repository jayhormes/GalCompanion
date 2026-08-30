Two ways to record what happens while you play, and playtime that follows you
between machines.

**Write the note while you are still in the game**

Pressing 📷 no longer takes the shot straight away. It opens a small text box
next to the bubble. Type a line, press 📷 again (or Enter, or the Send button)
and the screenshot goes to Trilium with your note attached. Type nothing and it
is just two clicks for a plain screenshot.

📝 works the same way, but writes to the "translation problems" sub-note, and it
attaches a screenshot too — a note about a bad line is hard to read later without
the line in front of you. Uncheck "attach screenshot" in the box for text only.

The box takes keyboard focus, so the game window is remembered when the box
opens and that window is what gets captured — not whatever is in front at the
time. The box is closed and the game brought back before the shot is taken, so
it never appears in the picture.

**One Trilium note per game per day**

Screenshots used to land in a single shared note. Now the day's journal note
gets one child per game:

```
2026-08-30
 └ <game name> 遊戲心得
     └ 翻譯問題
```

The day note comes from Trilium's own calendar, so it fits an existing journal
without any parent note to configure. Turn off "one note per game" to go back to
a single shared note, or put `{game}` in the title to choose where the name goes.

**Playtime and a calendar of what you played**

Every session is recorded as a start time and a length. The sidebar shows a year
of activity as a calendar grid, plus your longest-played games. Playnite's
playtime is set to the larger of its current value and the recorded total, so
imported playtime from Steam or GOG is never wiped.

**Playtime that syncs between machines**

Optional, over the same rclone remote as save sync. Each machine writes only its
own file and reads the union of all of them, so two machines playing the same
game never overwrite each other and there is nothing to resolve. Sessions are
pulled when Playnite starts and pushed when a game ends or Playnite closes.

**Import from LunaTranslator**

Extensions menu, "從 LunaTranslator 匯入遊玩時間". A one-time move: it reads
LunaTranslator's session database, matches games by executable path (including
games launched through Locale Emulator), and writes the history into Playnite
and into the activity calendar.

The match is shown before anything is written, grouped by what will happen to
each game. Playnite goes through the official API, so the library is never
touched directly and Playnite can stay open. The session log is backed up first,
and running the import twice does not double count.

The v2.0.0 release shipped this as a standalone LunaImport.exe. That tool read
the library as one JSON file per game, which is the Playnite 8 layout — on any
current Playnite it could not find the library at all. It has been removed.

**Also**

- Settings moved into Playnite's add-on settings; the config file is still read.
- A button to bring the bubble back to the centre of the screen when it ends up
  off-screen after a resolution or monitor change.
