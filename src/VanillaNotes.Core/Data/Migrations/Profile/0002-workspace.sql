-- Where the person was in the workspace of the profile when it was last left: the note open,
-- and the folder chosen in the sidebar, or the notes in no folder.
-- Documented in docs/02-data/: change both together.

CREATE TABLE workspace (
  id             INTEGER PRIMARY KEY CHECK (id = 1),
  note_id        INTEGER REFERENCES notes(id)   ON DELETE SET NULL,
  folder_id      INTEGER REFERENCES folders(id) ON DELETE SET NULL,
  without_folder INTEGER NOT NULL DEFAULT 0 CHECK (without_folder IN (0, 1)),
  updated_at     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  CHECK (folder_id IS NULL OR without_folder = 0)
) STRICT;
