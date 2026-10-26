-- The catalog: the list of the profiles of this machine. Not encrypted.
-- Documented in docs/02-data/: change both together.

CREATE TABLE profiles (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  name         TEXT    NOT NULL COLLATE NOCASE UNIQUE CHECK (length(name) BETWEEN 1 AND 50),
  password_key TEXT,
  recovery_key TEXT,
  created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
  CHECK ((password_key IS NULL) = (recovery_key IS NULL))
) STRICT;
