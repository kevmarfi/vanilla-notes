-- A note pinned comes first in the list of its profile.
-- Documented in docs/02-data/: change both together.

ALTER TABLE notes ADD COLUMN pinned INTEGER NOT NULL DEFAULT 0 CHECK (pinned IN (0, 1));
