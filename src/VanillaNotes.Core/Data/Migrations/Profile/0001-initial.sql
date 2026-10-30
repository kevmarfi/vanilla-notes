-- The database of one profile: everything it owns, and its search index.
-- Encrypted with the key of the profile when it has a password.
-- Documented in docs/02-data/: change both together.

CREATE TABLE folders (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  name       TEXT    NOT NULL COLLATE NOCASE UNIQUE CHECK (length(name) BETWEEN 1 AND 100),
  created_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- A note with a deleted_at is in the trash, kept until it is put back or deleted for good.
-- A note pinned comes first in the list of its profile.
CREATE TABLE notes (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  folder_id  INTEGER REFERENCES folders(id) ON DELETE SET NULL,
  title      TEXT    NOT NULL CHECK (length(title) BETWEEN 1 AND 200),
  content    TEXT    NOT NULL DEFAULT '' CHECK (length(content) <= 100000),
  version    INTEGER NOT NULL DEFAULT 1 CHECK (version >= 1),
  created_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  updated_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  deleted_at TEXT,
  pinned     INTEGER NOT NULL DEFAULT 0 CHECK (pinned IN (0, 1))
) STRICT;

CREATE TABLE tags (
  id   INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT    NOT NULL UNIQUE CHECK (length(name) BETWEEN 1 AND 30)
) STRICT;

CREATE TABLE note_tags (
  note_id INTEGER NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
  tag_id  INTEGER NOT NULL REFERENCES tags(id)  ON DELETE CASCADE,
  PRIMARY KEY (note_id, tag_id)
) STRICT;

CREATE TABLE attachments (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  note_id      INTEGER NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
  filename     TEXT    NOT NULL CHECK (length(filename) BETWEEN 1 AND 255),
  mime_type    TEXT    NOT NULL,
  size         INTEGER NOT NULL CHECK (size BETWEEN 1 AND 5242880),
  storage_name TEXT    NOT NULL UNIQUE,
  created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE settings (
  id         INTEGER PRIMARY KEY CHECK (id = 1),
  settings   TEXT    NOT NULL DEFAULT '{}',
  updated_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

-- Where the person was in the workspace of the profile when it was last left: the note open,
-- and the folder chosen in the sidebar, or the notes in no folder.
CREATE TABLE workspace (
  id             INTEGER PRIMARY KEY CHECK (id = 1),
  note_id        INTEGER REFERENCES notes(id)   ON DELETE SET NULL,
  folder_id      INTEGER REFERENCES folders(id) ON DELETE SET NULL,
  without_folder INTEGER NOT NULL DEFAULT 0 CHECK (without_folder IN (0, 1)),
  updated_at     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  CHECK (folder_id IS NULL OR without_folder = 0)
) STRICT;

CREATE INDEX idx_notes_updated    ON notes (updated_at DESC);
CREATE INDEX idx_notes_folder     ON notes (folder_id);
CREATE INDEX idx_note_tags_tag    ON note_tags (tag_id);
CREATE INDEX idx_attachments_note ON attachments (note_id);

-- The list of notes, out of the trash, in each order it can be asked for: the pinned first.
-- Without them a page of the list reads and sorts every note of the profile. The trash has an
-- index of its own, over the deleted notes only: one over every note was taken by the engine for
-- the list too, since "not in the trash" looked rare to it, and the list was sorted again.
CREATE INDEX idx_notes_trash        ON notes (deleted_at DESC, id DESC) WHERE deleted_at IS NOT NULL;
CREATE INDEX idx_notes_list_updated ON notes (pinned DESC, updated_at DESC, id DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_notes_list_created ON notes (pinned DESC, created_at DESC, id DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_notes_list_title   ON notes (pinned DESC, title COLLATE NOCASE, id DESC) WHERE deleted_at IS NULL;

CREATE VIRTUAL TABLE notes_fts USING fts5(
  title,
  content,
  content = 'notes',
  content_rowid = 'id',
  tokenize = 'unicode61 remove_diacritics 2'
);

CREATE TRIGGER notes_fts_insert AFTER INSERT ON notes BEGIN
  INSERT INTO notes_fts (rowid, title, content) VALUES (new.id, new.title, new.content);
END;

CREATE TRIGGER notes_fts_delete AFTER DELETE ON notes BEGIN
  INSERT INTO notes_fts (notes_fts, rowid, title, content) VALUES ('delete', old.id, old.title, old.content);
END;

CREATE TRIGGER notes_fts_update AFTER UPDATE OF title, content ON notes BEGIN
  INSERT INTO notes_fts (notes_fts, rowid, title, content) VALUES ('delete', old.id, old.title, old.content);
  INSERT INTO notes_fts (rowid, title, content) VALUES (new.id, new.title, new.content);
END;
