-- The trash: a note deleted is kept, with the moment it was deleted, until it is put back or
-- deleted for good. A note with no moment is not in the trash.
-- Documented in docs/02-data/: change both together.

ALTER TABLE notes ADD COLUMN deleted_at TEXT;

CREATE INDEX idx_notes_deleted ON notes (deleted_at);
