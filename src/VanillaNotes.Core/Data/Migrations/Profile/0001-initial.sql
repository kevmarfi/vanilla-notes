-- The database of one profile: everything it owns, and its search index.
-- Encrypted with the key of the profile when it has a password.
-- Documented in docs/02-data/: change both together.

CREATE TABLE folders (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  name       TEXT    NOT NULL COLLATE NOCASE UNIQUE CHECK (length(name) BETWEEN 1 AND 100),
  created_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
) STRICT;

CREATE TABLE notes (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  folder_id  INTEGER REFERENCES folders(id) ON DELETE SET NULL,
  title      TEXT    NOT NULL CHECK (length(title) BETWEEN 1 AND 200),
  content    TEXT    NOT NULL DEFAULT '' CHECK (length(content) <= 100000),
  version    INTEGER NOT NULL DEFAULT 1 CHECK (version >= 1),
  created_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  updated_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
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

CREATE INDEX idx_notes_updated    ON notes (updated_at DESC);
CREATE INDEX idx_notes_folder     ON notes (folder_id);
CREATE INDEX idx_note_tags_tag    ON note_tags (tag_id);
CREATE INDEX idx_attachments_note ON attachments (note_id);

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
