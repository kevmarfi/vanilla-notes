-- The list of notes, out of the trash, in each order it can be asked for: the pinned first.
-- Without them a page of the list reads and sorts every note of the profile.
-- Documented in docs/02-data/: change both together.

CREATE INDEX idx_notes_list_updated ON notes (pinned DESC, updated_at DESC, id DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_notes_list_created ON notes (pinned DESC, created_at DESC, id DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_notes_list_title   ON notes (pinned DESC, title COLLATE NOCASE, id DESC) WHERE deleted_at IS NULL;
